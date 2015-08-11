using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceModel.Description;
using System.Threading;
using System.Windows;
using System.Xml;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Services;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Discovery;
using System.Threading.Tasks;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

//For login, I used microsoft's code which is in CRM SDK.

namespace CemYabansu.PublishInCrm.Windows
{
    public partial class UserCredential
    {
        private Dictionary<string, string> _organizationsDictionary;
        public string ConnectionString { get; set; }

        private string _projectPath;
        private string _savePath;
        private string _username;
        private string _password;
        private string _domain;
        private string _serverUrl;
        private string _portNumber;
        private bool _isSsl;

        public UserCredential(string solutionPath)
        {
            InitializeComponent();

            _projectPath = solutionPath;
            _savePath = Path.GetDirectoryName(solutionPath);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrganizationUrl = _organizationsDictionary[(string)OrganizationsComboBox.SelectedValue];
            System.Threading.Tasks.Task.Factory.StartNew(() => SaveConnectionString(selectedOrganizationUrl));
        }

        private async void SaveConnectionString(string selectedOrganizationUrl)
        {
            SetEnableToUIElement(SaveButton, false);
            var connectionString = string.Format("Server={0}; Domain={1}; Username={2}; Password={3}",
                                                    selectedOrganizationUrl, _domain, _username, _password);

            SetActivateToConnectionProgressRing(true);
            SetConnectionStatus("Testing connection..");

            var result = await System.Threading.Tasks.Task.FromResult(TestConnection(connectionString));

            SetActivateToConnectionProgressRing(false);
            if (!result)
            {
                SetConnectionStatus("Connection failed.");
                return;
            }

            WriteConnectionStringToFile(_projectPath, ConnectionString, _savePath);
            Dispatcher.Invoke(Close);
        }

        private void WriteConnectionStringToFile(string projectName, string connectionString, string path)
        {
            var xmlDoc = new XmlDocument();
            var rootNode = xmlDoc.CreateElement("connectionString");
            xmlDoc.AppendChild(rootNode);

            var nameNode = xmlDoc.CreateElement("name");
            nameNode.InnerText = projectName;
            rootNode.AppendChild(nameNode);

            var connectionStringNode = xmlDoc.CreateElement("string");
            connectionStringNode.InnerText = connectionString;
            rootNode.AppendChild(connectionStringNode);

            xmlDoc.Save(path + "\\credential.xml");
        }

        public bool TestConnection(string server, string domain, string username, string password)
        {
            var connectionString = string.Format("Server={0}; Domain={1}; Username={2}; Password={3}",
                                                    server, domain, username, password);
            return TestConnection(connectionString);
        }

        public bool TestConnection(string connectionString)
        {
            SetActivateToConnectionProgressRing(true);
            try
            {
                var crmConnection = CrmConnection.Parse(connectionString);
                //to escape "another assembly" exception
                crmConnection.ProxyTypesAssembly = Assembly.GetExecutingAssembly();
                OrganizationService orgService;
                using (orgService = new OrganizationService(crmConnection))
                {
                    orgService.Execute(new WhoAmIRequest());
                    ConnectionString = connectionString;
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void GetOrganizationsButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareUiForGetOrganizations();

            System.Threading.Tasks.Task.Factory.StartNew(GetOrganizationsTask);

        }

        private void PrepareUiForGetOrganizations()
        {
            SetConnectionStatus("");
            SetActivateToProgressRing(true);

            _serverUrl = ServerTextBox.Text;
            _portNumber = PortTextBox.Text;
            _isSsl = (bool)SslCheckBox.IsChecked;
            _username = UsernameTextBox.Text;
            _password = PasswordTextBox.Password;
            _domain = DomainTextBox.Text;

            OrganizationsComboBox.Items.Clear();
            OrganizationsComboBox.SelectedIndex = -1;
            OrganizationsComboBox.IsEnabled = false;
            SaveButton.IsEnabled = false;
            GetOrganizationsButton.IsEnabled = false;
        }

        private async void GetOrganizationsTask()
        {
            var result = await System.Threading.Tasks.Task.FromResult(GetOrganizations());

            SetEnableToUIElement(SaveButton, result.Item1);
            EnableComboBox(result.Item1);
            SetEnableToUIElement(GetOrganizationsButton, true);
            SetConnectionStatus(result.Item2);
        }

        private Tuple<bool, string> GetOrganizations()
        {
            try
            {
                //creating discovery url
                var serverUrl = _serverUrl;
                if (!serverUrl.StartsWith("http"))
                {
                    serverUrl = string.Format("{0}{1}", _isSsl ? "https://" : "http://", serverUrl);
                }
                var portNumber = string.IsNullOrWhiteSpace(_portNumber) ? "" : ":" + _portNumber;
                var discoveryUri = new Uri(string.Format("{0}{1}/XrmServices/2011/Discovery.svc", serverUrl, portNumber));

                //getting organizations with 10 seconds timeout
                OrganizationDetailCollection orgs = new OrganizationDetailCollection();
                object monitorSync = new object();
                Action longMethod = () => GetOrganizationCollection(monitorSync, discoveryUri, out orgs);
                bool timedOut;
                lock (monitorSync)
                {
                    longMethod.BeginInvoke(null, null);
                    timedOut = !Monitor.Wait(monitorSync, TimeSpan.FromSeconds(15)); // waiting 15 secs
                }
                if (timedOut)
                {
                    return Tuple.Create(false, "Error : Timeout(15 s)");
                }
                if (orgs == null)
                {
                    return Tuple.Create(false, "Error : Organization not found.");
                }
                _organizationsDictionary = new Dictionary<string, string>();
                foreach (var org in orgs)
                {
                    AddItemToComboBox(org.FriendlyName);
                    _organizationsDictionary.Add(org.FriendlyName, org.Endpoints[EndpointType.WebApplication]);
                }
                return Tuple.Create(true, "Successfully connected.");
            }
            catch (Exception)
            {
                return Tuple.Create(false, "Error : Connection failed.");
            }
        }

        private void GetOrganizationCollection(object monitorSync, Uri discoveryUri, out OrganizationDetailCollection orgs)
        {
            IServiceManagement<IDiscoveryService> serviceManagement;
            try
            {
                serviceManagement = ServiceConfigurationFactory.CreateManagement<IDiscoveryService>(discoveryUri);
            }
            catch (Exception)
            {
                orgs = null;
                return;
            }
            AuthenticationProviderType endpointType = serviceManagement.AuthenticationType;

            AuthenticationCredentials authCredentials = GetCredentials(serviceManagement, endpointType);

            using (DiscoveryServiceProxy discoveryProxy =
                    GetProxy<IDiscoveryService, DiscoveryServiceProxy>(serviceManagement, authCredentials))
            {
                orgs = DiscoverOrganizations(discoveryProxy);
            }
            lock (monitorSync)
            {
                Monitor.Pulse(monitorSync);
            }
        }


        private OrganizationDetailCollection DiscoverOrganizations(IDiscoveryService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            RetrieveOrganizationsRequest orgRequest = new RetrieveOrganizationsRequest();
            RetrieveOrganizationsResponse orgResponse =
                (RetrieveOrganizationsResponse)service.Execute(orgRequest);

            return orgResponse.Details;
        }

        private TProxy GetProxy<TService, TProxy>(IServiceManagement<TService> serviceManagement,
    AuthenticationCredentials authCredentials)
            where TService : class
            where TProxy : ServiceProxy<TService>
        {
            Type classType = typeof(TProxy);

            if (serviceManagement.AuthenticationType !=
                AuthenticationProviderType.ActiveDirectory)
            {
                AuthenticationCredentials tokenCredentials =
                    serviceManagement.Authenticate(authCredentials);
                // Obtain discovery/organization service proxy for Federated, LiveId and OnlineFederated environments. 
                // Instantiate a new class of type using the 2 parameter constructor of type IServiceManagement and SecurityTokenResponse.
                return (TProxy)classType
                    .GetConstructor(new Type[] { typeof(IServiceManagement<TService>), typeof(SecurityTokenResponse) })
                    .Invoke(new object[] { serviceManagement, tokenCredentials.SecurityTokenResponse });
            }

            // Obtain discovery/organization service proxy for ActiveDirectory environment.
            // Instantiate a new class of type using the 2 parameter constructor of type IServiceManagement and ClientCredentials.
            return (TProxy)classType
                .GetConstructor(new Type[] { typeof(IServiceManagement<TService>), typeof(ClientCredentials) })
                .Invoke(new object[] { serviceManagement, authCredentials.ClientCredentials });
        }


        //<snippetAuthenticateWithNoHelp2>
        /// <summary>
        /// Obtain the AuthenticationCredentials based on AuthenticationProviderType.
        /// </summary>
        /// <param name="service">A service management object.</param>
        /// <param name="endpointType">An AuthenticationProviderType of the CRM environment.</param>
        /// <returns>Get filled credentials.</returns>
        private AuthenticationCredentials GetCredentials<TService>(IServiceManagement<TService> service, AuthenticationProviderType endpointType)
        {
            AuthenticationCredentials authCredentials = new AuthenticationCredentials();

            switch (endpointType)
            {
                case AuthenticationProviderType.ActiveDirectory:
                    authCredentials.ClientCredentials.Windows.ClientCredential =
                        new NetworkCredential(_username, _password, _domain);
                    break;
                case AuthenticationProviderType.LiveId:
                    authCredentials.ClientCredentials.UserName.UserName = _username;
                    authCredentials.ClientCredentials.UserName.Password = _password;
                    authCredentials.SupportingCredentials = new AuthenticationCredentials
                    {
                        ClientCredentials = Microsoft.Crm.Services.Utility.DeviceIdManager.LoadOrRegisterDevice()
                    };
                    break;
                default: // For Federated and OnlineFederated environments.                    
                    authCredentials.ClientCredentials.UserName.UserName = _username;
                    authCredentials.ClientCredentials.UserName.Password = _password;
                    // For OnlineFederated single-sign on, you could just use current UserPrincipalName instead of passing user name and password.
                    // authCredentials.UserPrincipalName = UserPrincipal.Current.UserPrincipalName;  // Windows Kerberos

                    // The service is configured for User Id authentication, but the user might provide Microsoft
                    // account credentials. If so, the supporting credentials must contain the device credentials.
                    if (endpointType == AuthenticationProviderType.OnlineFederation)
                    {
                        IdentityProvider provider = service.GetIdentityProvider(authCredentials.ClientCredentials.UserName.UserName);
                        if (provider != null && provider.IdentityProviderType == IdentityProviderType.LiveId)
                        {
                            authCredentials.SupportingCredentials = new AuthenticationCredentials();
                            authCredentials.SupportingCredentials.ClientCredentials =
                                Microsoft.Crm.Services.Utility.DeviceIdManager.LoadOrRegisterDevice();
                        }
                    }
                    break;
            }

            return authCredentials;
        }

        public void SetConnectionStatus(string text)
        {
            Dispatcher.Invoke(() => ConnectionStatusLabel.Content = text);
            SetActivateToProgressRing(false);
        }

        public void SetActivateToProgressRing(bool isActive)
        {
            Dispatcher.Invoke(() => ProgressRing.IsActive = isActive);
        }

        public void SetActivateToConnectionProgressRing(bool isActive)
        {
            Dispatcher.Invoke(() => ConnectionProgressRing.IsActive = isActive);
        }

        public void AddItemToComboBox(string item)
        {
            Dispatcher.Invoke(() => OrganizationsComboBox.Items.Add(item));
        }

        private void EnableComboBox(bool isEnable)
        {
            Dispatcher.Invoke(() => OrganizationsComboBox.SelectedIndex = isEnable ? 0 : -1);
            Dispatcher.Invoke(() => OrganizationsComboBox.IsEnabled = isEnable);
        }

        private void SetEnableToUIElement(UIElement button, bool isEnable)
        {
            Dispatcher.Invoke(() => button.IsEnabled = isEnable);
        }
    }
}