using System.Reflection;

namespace MyReplayLibrary;

public class ScannedFileList {
    private const string FileName = "scanned_file_list.txt";
    private readonly HashSet<string> _scannedFiles;
    private readonly string _filePath;

    public ScannedFileList() {
        //var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(basePath, FileName);
        if (!File.Exists(_filePath)) {
            File.AppendAllText(_filePath, "");
        }

        _scannedFiles = [.. File.ReadAllLines(_filePath)];
    }

    public bool Contains(string replay) {
        return _scannedFiles.Contains(replay);
    }

    public void Add(string replay) {
        _scannedFiles.Add(replay);
        File.AppendAllLines(_filePath, [replay]);
    }
}
