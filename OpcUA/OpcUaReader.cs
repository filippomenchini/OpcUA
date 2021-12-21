using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using OpcUA.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpcUA {
    public class OpcUaReader {
        public int NodeCount { get; private set; }
        public string ApplicationName { get; private set; }
        public string ConnectionString { get; private set; }
        public Session Session { get; private set; }
        public EndpointDescription Endpoint { get; private set; }
        public ApplicationInstance Application { get; private set; }
        public Dictionary<string, Type> ReferencesToMap { get; private set; }
        public ReferenceDescriptionCollection MappedReferences { get; private set; }

        private OpcUaReader(string connectionString, string applicationName) {
            if (connectionString.IsNullOrEmpty()) throw new ArgumentNullException("connectionString", "connectionString is null or empty.");
            if (applicationName.IsNullOrEmpty()) throw new ArgumentNullException("applicationName", "connectionString is null or empty.");

            ConnectionString = connectionString;
            ApplicationName = applicationName;

            InitSession();
        }

        private void InitSession() {
            ApplicationConfiguration config = GetApplicationConfiguration();

            ApplicationInstance application = GetApplicationInstance(config);

            Endpoint = CoreClientUtils.SelectEndpoint(ConnectionString, false, 15000);
            Session = Session.Create(config, new ConfiguredEndpoint(null, Endpoint, EndpointConfiguration.Create(config)), false, "", 60000, null, null).GetAwaiter().GetResult();
            Application = application;
        }

        private ApplicationConfiguration GetApplicationConfiguration() {
            ApplicationConfiguration config = new ApplicationConfiguration() {
                ApplicationName = this.ApplicationName,
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

            return config;
        }

        private ApplicationInstance GetApplicationInstance(ApplicationConfiguration config) {
            if (config == null) throw new ArgumentNullException("config", "config is null.");
            ApplicationInstance application = new ApplicationInstance {
                ApplicationName = ApplicationName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            application.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();

            return application;
        }

        private void MapReferencesToMap(ReferenceDescription startRd = null) {
            if (Session == null) throw new ArgumentNullException("session", "session is null.");
            NodeId nodeToBrowse = startRd != null ? ExpandedNodeId.ToNodeId(startRd.NodeId, Session.NamespaceUris) : ObjectIds.ObjectsFolder;
            Session.Browse(null, null, nodeToBrowse, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out byte[] cp, out ReferenceDescriptionCollection refs);

            if (MappedReferences == null) MappedReferences = new ReferenceDescriptionCollection();

            foreach (ReferenceDescription rd in refs) {
                NodeCount += 1;
                if (ReferencesToMap.ContainsKey(rd.DisplayName.Text) && !MappedReferences.Any(x => x.DisplayName.Text == rd.DisplayName.Text)) MappedReferences.Add(rd);
                MapReferencesToMap(rd);
            }
        }

        public static OpcUaReader Create(string connectionString, string applicationName) { return new OpcUaReader(connectionString, applicationName); }
        public OpcUaReader WithRefrencesToMap(Dictionary<string, Type> referencesToMap) {
            ReferencesToMap = referencesToMap;
            MapReferencesToMap();
            return this;
        }

        public object GetVariableValue(string referenceKey) {
            if (referenceKey.IsNullOrEmpty()) throw new ArgumentNullException("referenceKey", "referenceKey is null or empty.");
            ReferenceDescription rd = MappedReferences.Where(x => x.DisplayName == referenceKey).SingleOrDefault();
            if (rd == null) throw new NullReferenceException("ReferenceDescription rd is null");
            object objOut = null;
            if (rd != null && rd.NodeId != null && rd.NodeClass == NodeClass.Variable) {
                NodeId n = ExpandedNodeId.ToNodeId(rd.NodeId, Session.NamespaceUris);
                DataValue dv = Session.ReadValue(n);
                if (dv != null) {
                    objOut = dv.GetValue(ReferencesToMap[rd.DisplayName.Text]);
                }
            }
            return objOut;
        }

    }
}
