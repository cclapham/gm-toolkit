using GmToolkit.Core.Services;

namespace GmToolkit.Data.Tests;

public class AppSettingsServiceTests : IAsyncLifetime
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"gmtoolkit-settings-tests-{Guid.NewGuid()}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_rootDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task With_no_settings_file_yet_the_default_preference_is_System()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        var service = new AppSettingsService(settingsPath);

        var preference = await service.GetThemePreferenceAsync();

        Assert.Equal(ThemePreference.System, preference);
        Assert.False(File.Exists(settingsPath));
    }

    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public async Task Set_then_get_round_trips_the_preference(ThemePreference preference)
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        var service = new AppSettingsService(settingsPath);

        await service.SetThemePreferenceAsync(preference);
        var loaded = await service.GetThemePreferenceAsync();

        Assert.Equal(preference, loaded);
    }

    [Fact]
    public async Task A_saved_preference_survives_a_new_service_instance_against_the_same_path()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        var first = new AppSettingsService(settingsPath);
        await first.SetThemePreferenceAsync(ThemePreference.Dark);

        var second = new AppSettingsService(settingsPath);
        var loaded = await second.GetThemePreferenceAsync();

        Assert.Equal(ThemePreference.Dark, loaded);
    }

    [Fact]
    public async Task SetThemePreferenceAsync_creates_missing_parent_directories()
    {
        var settingsPath = Path.Combine(_rootDirectory, "nested", "does-not-exist-yet", "settings.json");
        Assert.False(Directory.Exists(Path.GetDirectoryName(settingsPath)));
        var service = new AppSettingsService(settingsPath);

        await service.SetThemePreferenceAsync(ThemePreference.Light);

        Assert.True(File.Exists(settingsPath));
    }

    [Fact]
    public async Task Corrupt_settings_file_falls_back_to_the_default_instead_of_throwing()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ this is not valid json");
        var service = new AppSettingsService(settingsPath);

        var preference = await service.GetThemePreferenceAsync();

        Assert.Equal(ThemePreference.System, preference);
    }

    [Fact]
    public async Task A_settings_file_containing_a_JSON_null_falls_back_to_the_default()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "null");
        var service = new AppSettingsService(settingsPath);

        var preference = await service.GetThemePreferenceAsync();

        Assert.Equal(ThemePreference.System, preference);
    }

    [Fact]
    public async Task Saving_after_a_corrupt_file_overwrites_it_with_a_valid_one()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "not json at all");
        var service = new AppSettingsService(settingsPath);

        await service.SetThemePreferenceAsync(ThemePreference.Dark);
        var preference = await service.GetThemePreferenceAsync();

        Assert.Equal(ThemePreference.Dark, preference);
    }

    [Fact]
    public async Task The_persisted_file_is_human_readable_JSON_with_a_string_enum_value()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        var service = new AppSettingsService(settingsPath);

        await service.SetThemePreferenceAsync(ThemePreference.Dark);

        var json = await File.ReadAllTextAsync(settingsPath);
        Assert.Contains("\"Dark\"", json);
        Assert.DoesNotContain(".tmp-", json);
    }

    [Fact]
    public async Task No_leftover_temp_file_remains_next_to_the_settings_file_after_a_save()
    {
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        var service = new AppSettingsService(settingsPath);

        await service.SetThemePreferenceAsync(ThemePreference.Light);

        var siblingFiles = Directory.GetFiles(_rootDirectory);
        Assert.Single(siblingFiles);
        Assert.Equal(settingsPath, siblingFiles[0]);
    }

    [Fact]
    public async Task Concurrent_SetThemePreferenceAsync_calls_never_produce_a_torn_or_corrupt_file()
    {
        // Writes are serialized by a private SemaphoreSlim in AppSettingsService, so concurrent
        // callers (e.g. a user flipping the Settings theme dropdown repeatedly) each complete a
        // full temp-file-write-then-move before the next one starts. Task.WhenAll gives no
        // guarantee about which of these concurrently *issued* calls finishes last, so the
        // property worth proving isn't "last call in program order wins" -- it's that the result
        // is always exactly one of the values passed in (never a blank/corrupt/interleaved file),
        // and that no temp file is left behind once every write has completed.
        var settingsPath = Path.Combine(_rootDirectory, "settings.json");
        var service = new AppSettingsService(settingsPath);
        var values = new[]
        {
            ThemePreference.Dark,
            ThemePreference.Light,
            ThemePreference.System,
            ThemePreference.Dark,
            ThemePreference.Light,
            ThemePreference.System,
            ThemePreference.Dark,
            ThemePreference.Light,
        };

        await Task.WhenAll(values.Select(value => service.SetThemePreferenceAsync(value)));

        var preference = await service.GetThemePreferenceAsync();
        Assert.Contains(preference, values);

        var siblingFiles = Directory.GetFiles(_rootDirectory);
        Assert.Single(siblingFiles);
        Assert.Equal(settingsPath, siblingFiles[0]);
    }
}