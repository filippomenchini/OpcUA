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

        private static int NodeCount { get; set; }
        private static string ApplicationName { get; set; } = "MyOpcUaClient";
        private static string ConnectionString { get; set; } = "opc.tcp://FILIPPO-HP.mshome.net:53530/OPCUA/SimulationServer";
        private static Dictionary<string, Type> DictVariables { get; set; } 
        private static ReferenceDescriptionCollection References { get; set; }

        static void Main(string[] args) {
            ApplicationConfiguration config = new ApplicationConfiguration() {
                ApplicationName = ApplicationName,
                ApplicationUri = Utils.Format(@"urn:{0}:{1}", System.Net.Dns.GetHostName(), ApplicationName),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = ApplicationName },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };
            config.Validate(ApplicationType.Client).GetAwaiter().GetResult();
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates) {
                config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }

            ApplicationInstance application = new ApplicationInstance {
                ApplicationName = ApplicationName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            application.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();

            EndpointDescription selectedEndpoint = CoreClientUtils.SelectEndpoint(ConnectionString, false, 15000);

            Console.WriteLine($"Step 1 - Create a session with your server: {selectedEndpoint.EndpointUrl} ");
            using (Session session = Session.Create(config, new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config)), false, "", 60000, null, null).GetAwaiter().GetResult()) {
                Console.WriteLine("Step 2 - Init DictVariables.");
                InitVariablesDictionary();

                Console.WriteLine("Step 3 - Browse the server namespace and map nodes in DictVariables.");
                Console.WriteLine("DisplayName: BrowseName, NodeClass");

                Stopwatch stopWatch = Stopwatch.StartNew();
                BrowseNodes(session);
                stopWatch.Stop();

                Console.WriteLine("------------------------------------------------------------------------------------");
                Console.WriteLine($"Elapsed milliseconds since server browsing started: {stopWatch.ElapsedMilliseconds} (milliseconds)");
                Console.WriteLine($"Nodes found: {NodeCount}");
                Console.WriteLine("------------------------------------------------------------------------------------");
                Console.WriteLine($"Nodes mapped: {References.Count}");

                foreach (ReferenceDescription rd in References) {
                    Console.WriteLine($"\tns={rd.NodeId.NamespaceIndex}; i={rd.NodeId.Identifier}: {rd.DisplayName.Text}");
                }

                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        private static void BrowseNodes(Session session, ReferenceDescription startRd = null, int level = 0) {
            NodeId nodeToBrowse = startRd != null ? ExpandedNodeId.ToNodeId(startRd.NodeId, session.NamespaceUris) : ObjectIds.ObjectsFolder;
            session.Browse(null, null, nodeToBrowse, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out byte[] cp, out ReferenceDescriptionCollection refs);

            if (References == null) References = new ReferenceDescriptionCollection();

            foreach (ReferenceDescription rd in refs) {
                NodeCount += 1;
                string indentation = string.Empty;
                for (int i = 0; i < level; i++) {
                    indentation += "\t";
                }
                Console.WriteLine("{0}{1}: {2}, {3}", indentation, rd.DisplayName, rd.BrowseName, rd.NodeClass);

                if (DictVariables.ContainsKey(rd.DisplayName.Text) && !References.Any(x => x.DisplayName.Text == rd.DisplayName.Text)) References.Add(rd);

                BrowseNodes(session, rd, level + 1);
            }
        }

        private static void InitVariablesDictionary() {
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
