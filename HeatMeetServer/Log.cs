namespace HeatMeetServer
{
    internal class Log
    {
        private static string logPath = "server_log.txt";
        private static readonly object logLock = new object();//lock bcs multiThread

        public static void startLog()
        {
            lock (logLock)
            {
                //no need to create manual file, File.AppendAllText is smart and intelligent

                //write header
                File.AppendAllText(logPath, $"{Environment.NewLine}##### SERVER STARTED: {DateTime.Now} #####{Environment.NewLine}");
            }
        }

        public static void Add_Log(string message)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            //console
            Console.WriteLine(message);

            //write file
            try
            {
                lock (logLock)
                {
                    File.AppendAllText(logPath, logLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("##### Error  Log: " + ex.Message + " #####");
            }
        }
    }
}
