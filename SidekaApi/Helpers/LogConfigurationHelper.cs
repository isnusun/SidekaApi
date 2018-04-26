using Serilog;
using Serilog.Core;
using Serilog.Filters;

namespace SidekaApi.Helpers
{
    public static class LogConfigurationHelper
    {
        public static Logger GetConfiguration()
        {
            var logger = new LoggerConfiguration();
            logger = GetConsoleConfiguration(logger);
            logger = GetRollingFileConfiguration(logger);
            return logger.CreateLogger();
        }

        public static LoggerConfiguration GetConsoleConfiguration(LoggerConfiguration logger)
        {
            return logger.WriteTo.Console();
        }

        public static LoggerConfiguration GetRollingFileConfiguration(LoggerConfiguration logger)
        {
            return logger
                .WriteTo.Logger(l => l
                    .MinimumLevel.Information()
                    .WriteTo.RollingFile("./Logs/Output-{Date}.txt", retainedFileCountLimit: 7))
                .WriteTo.Logger(l => l
                    .MinimumLevel.Warning()
                    .WriteTo.RollingFile("./Logs/Error-{Date}.txt"));
        }

        public static LoggerConfiguration GetClassLoggerConfiguration<T>(LoggerConfiguration logger)
        {
            return logger
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly(Matching.FromSource<T>())
                    .WriteTo.RollingFile("./Logs/" + typeof(T).Name + "-{Date}.txt")
                );
        }
    }
}