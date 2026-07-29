using System.Runtime.CompilerServices;

// Lets tests exercise DatabaseExceptionTranslator's translation logic directly, in isolation from
// a live GmToolkitDatabase/repository chain (see DatabaseExceptionTranslatorTests for why: some of
// that logic -- the file-missing check in particular -- needs platform-agnostic coverage that
// doesn't depend on deleting a file out from under a live OS-level SQLite file lock, which behaves
// differently on Windows than on Linux/macOS), without making the translator itself public API.
[assembly: InternalsVisibleTo("GmToolkit.Data.Tests")]