using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.ServiceModel.Security.Tokens;
using System.Text;
using System.Threading.Tasks;

namespace NXP3_ReadInputFiles.Helper
{
    public class InfoShareWSHelper
    {
        #region Properties
        /// <summary>
        /// Root uri for the Web Services
        /// </summary>
        public Uri ServiceUri { get; private set; }
        /// <summary>
        /// Username to use with usernamemixed
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// Password to use with usernamemixed
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// Timeout to control Send/Receive timeouts of WCF when issuing a token
        /// </summary>
        public TimeSpan? IssueTimeout { get; set; }
        /// <summary>
        /// Timeout to control Send/Receive timeouts of WCF for InfoShareWS proxies
        /// </summary>
        public TimeSpan? ServiceTimeout { get; set; }

        /// <summary>
        /// Checks whether the token is issued and still valid
        /// </summary>
        public bool IsExpired
        {
            get
            {
                if (this.issuedToken == null)
                {
                    return true;
                }
                return this.issuedToken.ValidTo.ToUniversalTime() <= DateTime.UtcNow;
            }
        }

        public bool IsWindowsAuthentication
        {
            get { return String.IsNullOrEmpty(Username); }
        }

        #endregion

        #region Fields
        private Binding proxyBinding;
        private ServiceEndpoint application25ServiceEndpoint;
        private ServiceEndpoint issuerServiceEndpoint;
        private GenericXmlSecurityToken issuedToken;

        private Dictionary<string, Uri> uris;
        #endregion

        #region Constructor

        public InfoShareWSHelper(Uri serviceUri)
        {
            ServiceUri = serviceUri;
            this.uris = new Dictionary<string, Uri>();
            //Mandatory
            uris.Add("Application25", new Uri(ServiceUri, "Wcf/API25/Application.svc"));
            //TODO Add additional relative endpoints here.
            uris.Add("Application20", new Uri(ServiceUri, "Wcf/API20/Application.svc"));
            //Endpoints used in the samples
            uris.Add("DocumentObj20", new Uri(ServiceUri, "Wcf/API20/DocumentObj.svc"));
            uris.Add("DocumentObj25", new Uri(ServiceUri, "Wcf/API25/DocumentObj.svc"));
            uris.Add("PublicationOutput20", new Uri(ServiceUri, "Wcf/API20/PublicationOutput.svc"));
            uris.Add("PublicationOutput25", new Uri(ServiceUri, "Wcf/API25/PublicationOutput.svc"));
            uris.Add("Baseline25", new Uri(ServiceUri, "Wcf/API25/Baseline.svc"));
            uris.Add("Folder25", new Uri(ServiceUri, "Wcf/API25/Folder.svc"));
            uris.Add("User25", new Uri(ServiceUri, "Wcf/API25/User.svc"));
            uris.Add("Search25", new Uri(ServiceUri, "Wcf/API25/Search.svc"));
            uris.Add("TranslationJob25", new Uri(ServiceUri, "Wcf/API25/TranslationJob.svc"));
            uris.Add("TranslationTemplate25", new Uri(ServiceUri, "Wcf/API25/TranslationTemplate.svc"));
            uris.Add("ConditionManagement10", new Uri(ServiceUri, "Wcf/API/ConditionManagement.svc"));
            uris.Add("Settings25", new Uri(ServiceUri, "Wcf/API25/Settings.svc"));
            uris.Add("EventMonitor25", new Uri(ServiceUri, "Wcf/API25/EventMonitor.svc"));
        }

        #endregion

        #region Binding and endpoints resolving based on wsdl files
        /// <summary>
        /// Using the Wcf/API25/Application.svc resolve
        /// 1. Binding and enpoints for the InfoShareWS endpoints
        /// 2. Look into the issuer elements to extract the issuer binding and endpoint
        /// </summary>
        public void Resolve()
        {
            Uri wsdlUri = new Uri(ServiceUri, uris["Application25"] + "?wsdl");
            var wsdlImporter = GetWsdlImporter(wsdlUri);
            this.application25ServiceEndpoint = wsdlImporter.ImportAllEndpoints().Single(p => p.Address.Uri.Scheme == ServiceUri.Scheme);
            this.proxyBinding = this.application25ServiceEndpoint.Binding;
            ApplyTimeout(this.application25ServiceEndpoint, ServiceTimeout);
            ApplyQuotas(this.application25ServiceEndpoint);

            FindIssuerEndpoint();
        }
        /// <summary>
        /// Find the wsdl importer
        /// </summary>
        /// <param name="wsdlUri">The wsdl uri</param>
        /// <returns>A wsdl importer</returns>
        private WsdlImporter GetWsdlImporter(Uri wsdlUri)
        {
            WSHttpBinding mexBinding = null;
            var mode = System.ServiceModel.Description.MetadataExchangeClientMode.HttpGet;
            if (wsdlUri.Scheme == Uri.UriSchemeHttp)
            {
                mexBinding = (WSHttpBinding)System.ServiceModel.Description.MetadataExchangeBindings.CreateMexHttpBinding();
            }
            else
            {
                mexBinding = (WSHttpBinding)System.ServiceModel.Description.MetadataExchangeBindings.CreateMexHttpsBinding();
            }
            mexBinding.MaxReceivedMessageSize = 2147483647;
            mexBinding.MaxBufferPoolSize = int.MaxValue;
            mexBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            mexBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
            mexBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            mexBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            mexBinding.ReaderQuotas.MaxDepth = 64;

            var mexClient = new System.ServiceModel.Description.MetadataExchangeClient(mexBinding);
            mexClient.MaximumResolvedReferences = int.MaxValue;

            var metadataSet = mexClient.GetMetadata(wsdlUri, mode);
            return new System.ServiceModel.Description.WsdlImporter(metadataSet);
        }

        /// <summary>
        /// Extract the Issuer endpoint and configure the appropriate one
        /// </summary>
        private void FindIssuerEndpoint()
        {
            CustomBinding customBinding = (CustomBinding)this.proxyBinding;
            IssuedSecurityTokenParameters protectionTokenParameters = null;
            //Based on the scheme dynamically extract the protection token parameters from a Property path string using reflection.
            //Writing the code requires to much casting. The paths are taken from the powershell scripts
            if (ServiceUri.Scheme == Uri.UriSchemeHttp)
            {
                protectionTokenParameters = (IssuedSecurityTokenParameters)GetPropertySequenceWithRelfection(this.application25ServiceEndpoint, "Binding.Elements[0].ProtectionTokenParameters.BootstrapSecurityBindingElement.ProtectionTokenParameters");
            }
            else
            {
                protectionTokenParameters = (IssuedSecurityTokenParameters)GetPropertySequenceWithRelfection(this.application25ServiceEndpoint, "Binding.Elements[0].EndpointSupportingTokenParameters.Endorsing[0].BootstrapSecurityBindingElement.EndpointSupportingTokenParameters.Endorsing[0]");
            }
            var issuerMetadataAddress = protectionTokenParameters.IssuerMetadataAddress;
            var issuerAddress = protectionTokenParameters.IssuerAddress;

            if (IsWindowsAuthentication)
            {
                //ADFS case
                //Repeat the endpoints extraction process for the STS endpoint
                var wsdlImporter = GetWsdlImporter(issuerMetadataAddress.Uri);
                var issuerEndpoints = wsdlImporter.ImportAllEndpoints();
                //Keep the windows mixed endpoint
                this.issuerServiceEndpoint = issuerEndpoints.Single(p => p.Address.Uri.AbsolutePath.EndsWith("adfs/services/trust/13/windowsmixed"));
            }
            else
            {
                //InfoshareSTS case
                //Repeat the endpoints extraction process for the STS endpoint
                var wsdlImporter = GetWsdlImporter(new Uri("https://nxp001.sdlproducts.com/ISHSTS/issue/wstrust/mex".Replace("/mex", "?wsdl")));
                var issuerEndpoints = wsdlImporter.ImportAllEndpoints();
                //Keep the username mixed endpoint
                this.issuerServiceEndpoint = issuerEndpoints.Single(p => p.Address.Uri.AbsolutePath.EndsWith("issue/wstrust/mixed/username"));
            }
            //Update the original binding as if we would do this manually in the configuration
            protectionTokenParameters.IssuerBinding = this.issuerServiceEndpoint.Binding;
            protectionTokenParameters.IssuerAddress = this.issuerServiceEndpoint.Address;


        }
        #endregion

        #region  Reflection Helper Methods
        /// <summary>
        /// Dynamically resolves a property sequence like found in powershell
        /// </summary>
        /// <param name="obj">The source object</param>
        /// <param name="sequence">The propert sequence</param>
        /// <returns>The value that is resolved from the last property</returns>
        private object GetPropertySequenceWithRelfection(object obj, string sequence)
        {
            var segments = sequence.Split('.');
            foreach (var segment in segments)
            {
                string propertyName = null;
                int? index = null;
                var indexOfArray = segment.IndexOf('[');
                //Check if the property is an array
                if (indexOfArray < 0)
                {
                    propertyName = segment;
                }
                else
                {
                    propertyName = segment.Substring(0, indexOfArray);
                    string indexPart = segment.Substring(indexOfArray);
                    index = int.Parse(indexPart.Substring(1, indexPart.Length - 2));
                }
                //Get the value of the property
                obj = GetPropertyWithReflection(obj, propertyName);
                //Get the value of the specific index from that property
                if (index.HasValue)
                {
                    obj = GetElementWithReflection(obj, index.Value);
                }

            }
            return obj;
        }
        private object GetPropertyWithReflection(object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName).GetValue(obj);
        }
        private object GetElementWithReflection(object obj, int index)
        {
            var items = (obj as IEnumerable).Cast<object>();
            if (index == 0)
            {
                return items.FirstOrDefault();
            }
            return items.Skip(index - 1).FirstOrDefault();
        }
        #endregion

        #region Token
        /// <summary>
        /// Issues the token
        /// Mostly copied from Service References
        /// </summary>
        public void IssueToken()
        {
            var requestSecurityToken = new RequestSecurityToken
            {
                RequestType = RequestTypes.Issue,
                AppliesTo = new EndpointReference(uris["Application25"].AbsoluteUri),
                KeyType = System.IdentityModel.Protocols.WSTrust.KeyTypes.Symmetric,
            };
            requestSecurityToken.TokenType = SamlSecurityTokenHandler.Assertion;
            //This should have worked directly but I don't know why it doesn't.
            //using (var factory = new WSTrustChannelFactory(this.issuerServiceEndpoint))
            using (var factory = new WSTrustChannelFactory((WS2007HttpBinding)this.issuerServiceEndpoint.Binding, this.issuerServiceEndpoint.Address))
            {
                ApplyCredentials(factory.Credentials);

                //Apply the connection timeout to the token issue process
                ApplyTimeout(factory.Endpoint, IssueTimeout);

                factory.TrustVersion = TrustVersion.WSTrust13;
                factory.Credentials.SupportInteractive = false;
                WSTrustChannel channel = null;

                try
                {
                    channel = (WSTrustChannel)factory.CreateChannel();
                    RequestSecurityTokenResponse requestSecurityTokenResponse;
                    this.issuedToken = channel.Issue(requestSecurityToken, out requestSecurityTokenResponse) as GenericXmlSecurityToken;

                }
                catch (Exception ex)
                {
                    throw;
                }
                finally
                {
                    if (channel != null)
                    {
                        channel.Abort();
                    }

                    factory.Abort();
                }
            }
        }
        /// <summary>
        /// Make sure that a token is present and hasn't expired
        /// </summary>
        public void GuardToken()
        {
            if (this.issuedToken == null)
            {
                IssueToken();
            }
        }
        #endregion

        #region WCF helpers
        private void ApplyCredentials(ClientCredentials clientCredentials)
        {
            if (String.IsNullOrEmpty(Username))
            {
                return;
            }
            clientCredentials.UserName.UserName = Username;
            clientCredentials.UserName.Password = Password;
        }
        private void ApplyTimeout(ServiceEndpoint endpoint, TimeSpan? timeout)
        {
            if (timeout != null)
            {
                endpoint.Binding.ReceiveTimeout = timeout.Value;
                endpoint.Binding.SendTimeout = timeout.Value;
            }

        }
        private void ApplyQuotas(ServiceEndpoint endpoint)
        {
            CustomBinding customBinding = (CustomBinding)endpoint.Binding;
            var textMessageEncoding = customBinding.Elements.Find<TextMessageEncodingBindingElement>();
            textMessageEncoding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            textMessageEncoding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
            textMessageEncoding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            textMessageEncoding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            textMessageEncoding.ReaderQuotas.MaxDepth = 64;

            var transport = customBinding.Elements.Find<TransportBindingElement>();
            transport.MaxReceivedMessageSize = 2147483647;
            transport.MaxBufferPoolSize = int.MaxValue;

        }
        #endregion

        #region Proxy Generators
        // TODO Add proxy generator functions

        #region Example for /Wcf/API25/Application.svc
        /// <summary>
        /// Create a /Wcf/API25/Application.svc proxy
        /// </summary>
        /// <returns>The proxy</returns>
        //public Application25ServiceReference.Application GetApplication25Channel()
        //{
        //    GuardToken();
        //    var client = new Application25ServiceReference.ApplicationClient(this.proxyBinding, new EndpointAddress(uris["Application25"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}
        #endregion

        /// <summary>
        /// Create a /Wcf/API25/Application.svc proxy
        /// </summary>
        /// <returns>The proxy</returns>
        public Application25ServiceReference.Application GetApplication25Channel()
        {
            GuardToken();
            var client = new Application25ServiceReference.ApplicationClient(this.proxyBinding, new EndpointAddress(uris["Application25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }

        /// <summary>
        /// Create a /Wcf/API20/Application.svc proxy
        /// </summary>
        /// <returns>The proxy</returns>
        //public Application20ServiceReference.Application GetApplication20Channel()
        //{
        //    GuardToken();
        //    var client = new Application20ServiceReference.ApplicationClient(this.proxyBinding, new EndpointAddress(uris["Application20"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        public PublicationOutput25ServiceReference.PublicationOutput GetPublication25Channel()
        {
            GuardToken();
            var client = new PublicationOutput25ServiceReference.PublicationOutputClient(this.proxyBinding, new EndpointAddress(uris["PublicationOutput25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }

        //public PublicationOutput20ServiceReference.PublicationOutput GetPublication20Channel()
        //{
        //    GuardToken();
        //    var client = new PublicationOutput20ServiceReference.PublicationOutputClient(this.proxyBinding, new EndpointAddress(uris["PublicationOutput20"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        public DocumentObj25ServiceReference.DocumentObj GetDocumentObj25Channel()
        {
            GuardToken();
            var client = new DocumentObj25ServiceReference.DocumentObjClient(this.proxyBinding, new EndpointAddress(uris["DocumentObj25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }

        //public DocumentObj20ServiceReference.DocumentObj GetDocumentObj20Channel()
        //{
        //    GuardToken();
        //    var client = new DocumentObj20ServiceReference.DocumentObjClient(this.proxyBinding, new EndpointAddress(uris["DocumentObj20"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        public Baseline25ServiceReference.Baseline GetBaseline25Channel()
        {
            GuardToken();
            var client = new Baseline25ServiceReference.BaselineClient(this.proxyBinding, new EndpointAddress(uris["Baseline25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }

        public Folder25ServiceReference.Folder GetFolder25Channel()
        {
            GuardToken();
            var client = new Folder25ServiceReference.FolderClient(this.proxyBinding, new EndpointAddress(uris["Folder25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }
        public ListOfValues25ServiceReference.ListOfValues GetListOfValues25Channel()
        {
            GuardToken();
            var client = new ListOfValues25ServiceReference.ListOfValuesClient(this.proxyBinding, new EndpointAddress(uris["ListOfValues25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }
        public User25ServiceReference.User GetUser25Channel()
        {
            GuardToken();
            var client = new User25ServiceReference.UserClient(this.proxyBinding, new EndpointAddress(uris["User25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }

        //public Search25ServiceReference.Search GetSearch25Channel()
        //{
        //    GuardToken();
        //    var client = new Search25ServiceReference.SearchClient(this.proxyBinding, new EndpointAddress(uris["Search25"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        //public TranslationJob25ServiceReference.TranslationJob GetTranslationJob25Channel()
        //{
        //    GuardToken();
        //    var client = new TranslationJob25ServiceReference.TranslationJobClient(this.proxyBinding, new EndpointAddress(uris["TranslationJob25"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        //public TranslationTemplate25ServiceReference.TranslationTemplate GetTranslationTemplate25Channel()
        //{
        //    GuardToken();
        //    var client = new TranslationTemplate25ServiceReference.TranslationTemplateClient(this.proxyBinding, new EndpointAddress(uris["TranslationTemplate25"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        //public ConditionManagement10ServiceReference.ConditionManagement GetConditionManagement10Channel()
        //{
        //    GuardToken();
        //    var client = new ConditionManagement10ServiceReference.ConditionManagementClient(this.proxyBinding, new EndpointAddress(uris["ConditionManagement10"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        public Settings25ServiceReference.Settings GetSettings25Channel()
        {
            GuardToken();
            var client = new Settings25ServiceReference.SettingsClient(this.proxyBinding, new EndpointAddress(uris["Settings25"]));
            return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        }

        //public EventMonitor25ServiceReference.EventMonitor GetEventMonitor25Channel()
        //{
        //    GuardToken();
        //    var client = new EventMonitor25ServiceReference.EventMonitorClient(this.proxyBinding, new EndpointAddress(uris["EventMonitor25"]));
        //    return client.ChannelFactory.CreateChannelWithIssuedToken(this.issuedToken);
        //}

        #endregion
    }
}
