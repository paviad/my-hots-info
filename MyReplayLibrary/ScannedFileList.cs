using System.IO;
using System.IO.IsolatedStorage;
using System.Text;

namespace MyReplayLibrary;

public class ScannedFileList {
    private const string FilePath = "scanned_file_list.txt";
    private readonly HashSet<string> _scannedFiles = [];
    private readonly IsolatedStorageFile _isoStore;

    public ScannedFileList() {
        _isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User
                                                 | IsolatedStorageScope.Application, null, null);
        using var f = _isoStore.OpenFile(FilePath, FileMode.OpenOrCreate);
        using var sr = new StreamReader(f);

        while (sr.ReadLine() is { } line) {
            _scannedFiles.Add(line);
        }
    }

    public bool Contains(string replay) {
        return _scannedFiles.Contains(replay);
    }

    public void Add(string replay) {
        _scannedFiles.Add(replay);
        using var f = _isoStore.OpenFile(FilePath, FileMode.Append);
        using var sr = new StreamWriter(f);
        sr.WriteLine(replay);
    }
}
