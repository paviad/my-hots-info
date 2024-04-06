namespace CascLibCore;

public class Logger {
    private static readonly FileStream fs = new(
        "debug.log",
        FileMode.Create,
        FileAccess.Write,
        FileShare.ReadWrite);

    private static readonly StreamWriter logger = new(fs) { AutoFlush = true };

    public static void WriteLine(string format, params object[] args) {
        logger.Write("[{0}]: ", DateTime.Now);
        logger.WriteLine(format, args);
    }
}
