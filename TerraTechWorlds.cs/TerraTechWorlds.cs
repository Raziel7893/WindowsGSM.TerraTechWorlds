using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class TerraTechWorlds : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.TerraTechWorlds", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting TerraTechWorlds Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.terratechworlds", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2321660"; // Game server appId Steam

        // - Standard Constructor and properties
        public TerraTechWorlds(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;


        // - Game server Fixed variables
        public override string StartPath => "TT2\\Binaries\\Win64\\TT2Server-Win64-Shipping.exe";
        public string FullName = "TerraTechWorlds Dedicated Server"; // Game server FullName
        public string ConfigFile = "dedicated_server_config.json"; // Game server FullName

        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7777"; // Default port

        public string Additional = "-log"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "7778"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        public void UpdateConfig()
        {
            string configFile = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, ConfigFile);
            StringBuilder sb = new StringBuilder();
            if (!File.Exists(configFile))
            {
                sb.Append($"{{" +
                    $"\r\n  \"Port\": 7777,  " +
                    $"\r\n  \"SlotCount\": 6," +
                    $"\r\n  \"Password\": \"\"" +
                    $"\r\n}}");
            }
            else
            {
                StreamReader sr = new StreamReader(configFile);
                var line = sr.ReadLine();
                while (line != null)
                {
                    if (line.Contains("Port\":"))
                        sb.Append($"\r\n  \"Port\": {serverData.ServerPort},  ");
                    else if (line.Contains("SlotCount\":"))
                        sb.Append($"\r\n  \"SlotCount\": {serverData.ServerMaxPlayer},");
                    else
                        sb.Append(line);
                    line = sr.ReadLine();
                }
            }

            File.WriteAllText(configFile, sb.ToString());
        }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            StringBuilder param = new StringBuilder();
            param.Append($" {serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (_serverData.EmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (_serverData.EmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(5000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
