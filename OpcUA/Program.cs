using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OpcUA {
    public class Program {
        private static string ApplicationName { get; set; } = "MyOpcUaClient";
        private static string ConnectionString { get; set; } = "opc.tcp://FILIPPO-HP.mshome.net:53530/OPCUA/SimulationServer";
        private static Dictionary<string, Type> DictVariables { get; set; }

        static void Main(string[] args) {
            InitDictVariables();
            OpcUaReader opcUaReader = OpcUaReader.Create(ConnectionString, ApplicationName).WithRefrencesToMap(DictVariables);
            Console.WriteLine("Going to read some variables");
            Console.WriteLine("-----------------------------");

            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (KeyValuePair<string, Type> variable in DictVariables) {
                Console.WriteLine($"{variable.Key}: {opcUaReader.GetVariableValue(variable.Key)}");
            }
            stopwatch.Stop();

            Console.WriteLine("-----------------------------");
            Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} (ms)");

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static void InitDictVariables() {
            DictVariables = new Dictionary<string, Type> {
                { "Counter", typeof(Int32) },
                { "Random", typeof(double) },
                { "Sawtooth", typeof(double) },
                { "Sinusoid", typeof(double) },
                { "Square", typeof(double) },
                { "Triangle", typeof(double) }
            };
        }
    }
}
