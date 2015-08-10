using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using CemYabansu.PublishInCrm.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.Crm.Sdk.Messages;

using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using OutputWindow = CemYabansu.PublishInCrm.Windows.OutputWindow;
using Thread = System.Threading.Thread;

namespace CemYabansu.PublishInCrm
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidPublishInCrmPkgString)]
    public sealed class PublishInCrmPackage : Package
    {
        private readonly string[] _expectedExtensions = { ".js", ".htm", ".html", ".css", ".png", ".jpg", ".jpeg", ".gif", ".xml" };

        private OutputWindow _outputWindow;
        private bool _error = false, _success = true;

        public PublishInCrmPackage()
        {
        }

        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this));
            base.Initialize();

            

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the publish in crm.
                CommandID publishInCrmCommandID = new CommandID(GuidList.guidPublishInCrmCmdSet, (int)PkgCmdIDList.cmdidPublishInCrm);
                MenuCommand publishInCrmMenuItem = new MenuCommand(PublishInCrmCallback, publishInCrmCommandID);
                mcs.AddCommand(publishInCrmMenuItem);

                // Create the command for the publish in crm(solution explorer).
                CommandID publishInCrmMultipleCommandID = new CommandID(GuidList.guidPublishInCrmCmdSet, (int)PkgCmdIDList.cmdidPublishInCrmMultiple);
                MenuCommand publishInCrmMultipleMenuItem = new MenuCommand(PublishInCrmMultipleCallback, publishInCrmMultipleCommandID);
                mcs.AddCommand(publishInCrmMultipleMenuItem);

            }
        }

        private void PublishInCrmMultipleCallback(object sender, EventArgs e)
        {
            PublishInCrm(true);
        }

        private void PublishInCrmCallback(object sender, EventArgs e)
        {
            PublishInCrm(false);
        }

        private void PublishInCrm(bool isFromSolutionExplorer)
        {
            _outputWindow = new OutputWindow();
            _outputWindow.Show();

            //getting selected files
            List<string> selectedFiles = GetSelectedFilesPath(isFromSolutionExplorer);

            //checking selected files extensions 
            var inValidFiles = CheckFilesExtension(selectedFiles);
            if (inValidFiles.Count > 0)
            {
                AddErrorToOutputWindow(string.Format("Invalid file extensions : {0}", string.Join(", ", inValidFiles)));
                AddErrorLineToOutputWindow(string.Format("Error : Invalid file extensions : \n\t- {0}", string.Join("\n\t- ", inValidFiles)));
                return;
            }

            //getting connection string
            var solutionPath = GetSolutionPath();
            var connectionString = GetConnectionString(solutionPath);
            if (connectionString == string.Empty)
            {
                SetConnectionLabelText("Connection string is not found.", _error);
                AddErrorLineToOutputWindow("Error : Connection string is not found.");

                var userCredential = new UserCredential(solutionPath);
                userCredential.ShowDialog();

                if (string.IsNullOrEmpty(userCredential.ConnectionString))
                {
                    SetConnectionLabelText("Connection failed.", _error);
                    AddErrorLineToOutputWindow("Error : Connection failed.");
                    return;
                }
                connectionString = userCredential.ConnectionString;
            }

            //updating/creating files one by one
            var thread = new Thread(o => UpdateWebResources(connectionString, selectedFiles));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <returns>List of selected file or active file</returns>
        private List<string> GetSelectedFilesPath(bool isFromSolutionExplorer)
        {
            var dte = (DTE2)GetService(typeof(SDTE));
            if (isFromSolutionExplorer)
            {
                var selectedItems = dte.SelectedItems;
                List<string> list = new List<string>();
                foreach (SelectedItem selItem in selectedItems)
                {
                    selItem.ProjectItem.Save();
                    list.Add(selItem.ProjectItem.FileNames[0]);
                }
                return list;
            }
            dte.ActiveDocument.Save();
            return new List<string> { dte.ActiveDocument.FullName };
        }

        /// <returns>List of invalid files according to _expectedExtensions</returns>
        private List<string> CheckFilesExtension(List<string> selectedFilesPaths)
        {
            var invalidFiles = new List<string>();
            for (var i = 0; i < selectedFilesPaths.Count; i++)
            {
                var selectedFileExtension = Path.GetExtension(selectedFilesPaths[i]);
                if (_expectedExtensions.All(t => t != selectedFileExtension))
                    invalidFiles.Add(Path.GetFileName(selectedFilesPaths[i]));
            }
            return invalidFiles;
        }

        private string GetSolutionPath()
        {
            var dte = (DTE2)GetService(typeof(SDTE));
            return dte.Solution.FullName;
        }

        /// <summary>
        /// This function reads the projectPath\credential.xml file.
        /// Gets the connection string and return it. If it doesn't exist, returns String.Empty
        /// </summary>
        /// <param name="projectPath">Path of project file.</param>
        private string GetConnectionString(string projectPath)
        {
            if (Path.HasExtension(projectPath))
                projectPath = Path.GetDirectoryName(projectPath);

            var filePath = projectPath + "\\credential.xml";

            while (!File.Exists(filePath))
            {
                projectPath = Directory.GetParent(projectPath).FullName;
                if (projectPath == Path.GetPathRoot(projectPath)) return string.Empty;
                filePath = projectPath + "\\credential.xml";
            }

            var reader = new StreamReader
                (
                new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read)
                );
            var doc = new XmlDocument();
            var xmlIn = reader.ReadToEnd();
            reader.Close();

            try
            {
                doc.LoadXml(xmlIn);
            }
            catch (XmlException)
            {
                return string.Empty;
            }

            var nodes = doc.GetElementsByTagName("string");
            foreach (XmlNode value in nodes)
            {
                var reStr = value.ChildNodes[0].Value;
                return reStr;
            }
            return string.Empty;
        }

        private void UpdateWebResources(string connectionString, List<string> selectedFiles)
        {
            try
            {
                var toBePublishedWebResources = new List<WebResource>();
                OrganizationService orgService;
                var crmConnection = CrmConnection.Parse(connectionString);
                //to escape "another assembly" exception
                crmConnection.ProxyTypesAssembly = Assembly.GetExecutingAssembly();
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                using (orgService = new OrganizationService(crmConnection))
                {
                    SetConnectionLabelText(string.Format("Connected to : {0}", crmConnection.ServiceUri), _success);
                    AddLineToOutputWindow(string.Format("Connected to : {0}", crmConnection.ServiceUri));

                    Dictionary<string, WebResource> toBeCreatedList;
                    Dictionary<string, WebResource> toBeUpdatedList;

                    GetWebresources(orgService, selectedFiles, out toBeCreatedList, out toBeUpdatedList);

                    CreateWebresources(toBeCreatedList, orgService, toBePublishedWebResources);

                    UpdateWebresources(toBeUpdatedList, orgService, toBePublishedWebResources);

                    PublishWebResources(orgService, toBePublishedWebResources);
                }
                stopwatch.Stop();
                AddLineToOutputWindow(string.Format("Time : {0}", stopwatch.Elapsed));
            }
            catch (Exception ex)
            {
                AddErrorToOutputWindow(ex.Message);
                AddErrorLineToOutputWindow("Error : " + ex.Message);
            }
        }

        private void UpdateWebresources(Dictionary<string, WebResource> toBeUpdatedList, OrganizationService orgService,
            List<WebResource> toBePublishedWebResources)
        {
            if (toBeUpdatedList.Count > 0)
            {
                StartUpdatingWebresources();
                foreach (var toBeUpdated in toBeUpdatedList)
                {
                    AddLineToOutputWindow(string.Format("Updating to webresource({0}) ..", Path.GetFileName(toBeUpdated.Key)));
                    UpdateWebResource(orgService, toBeUpdated.Value, toBeUpdated.Key);
                    AddLineToOutputWindow(string.Format("{0} is updated.", toBeUpdated.Value.Name));
                    toBePublishedWebResources.Add(toBeUpdatedList[toBeUpdated.Key]);
                }
                FinishUpdatingWebresources(_success);
            }
        }

        private void CreateWebresources(Dictionary<string, WebResource> toBeCreatedList, OrganizationService orgService,
            List<WebResource> toBePublishedWebResources)
        {
            if (toBeCreatedList.Count > 0)
            {
                StartCreatingWebresources();
                List<string> keys = new List<string>(toBeCreatedList.Keys);
                foreach (var key in keys)
                {
                    AddLineToOutputWindow(string.Format("Creating new webresource({0})..", Path.GetFileName(key)));
                    toBeCreatedList[key] = CreateWebResource(Path.GetFileName(key), orgService, key);
                    if (toBeCreatedList[key] == null)
                    {
                        AddLineToOutputWindow(string.Format("Creating new webresource({0}) is cancelled.", Path.GetFileName(key)));
                        continue;
                    }
                    AddLineToOutputWindow(string.Format("{0} is created.", Path.GetFileName(key)));
                    toBePublishedWebResources.Add(toBeCreatedList[key]);
                }
                FinishCreatingWebresources(_success);
            }
        }

        private void GetWebresources(OrganizationService orgService, List<string> selectedFiles, out Dictionary<string, WebResource> toBeCreatedList, out Dictionary<string, WebResource> toBeUpdatedList)
        {
            StartGettingWebresources();
            toBeCreatedList = new Dictionary<string, WebResource>();
            toBeUpdatedList = new Dictionary<string, WebResource>();
            for (int i = 0; i < selectedFiles.Count; i++)
            {
                var fileName = Path.GetFileName(selectedFiles[i]);
                var choosenWebresource = GetWebresource(orgService, fileName);
                if (choosenWebresource == null)
                {
                    AddErrorLineToOutputWindow(string.Format("Error : {0} is not exist in CRM.", fileName));
                    toBeCreatedList.Add(selectedFiles[i], null);
                }
                else
                {
                    toBeUpdatedList.Add(selectedFiles[i], choosenWebresource);
                }
            }
            FinishGettingWebresources(_success);
        }

        /// <returns>Webresource which has equal name with "filename" which with or without extension</returns>
        private WebResource GetWebresource(OrganizationService orgService, string filename)
        {
            var webresourceResult = WebresourceResult(orgService, filename);
            if (webresourceResult.Entities.Count == 0)
            {
                filename = Path.GetFileNameWithoutExtension(filename);
                webresourceResult = WebresourceResult(orgService, filename);
                if (webresourceResult.Entities.Count == 0)
                    return null;
            }

            return new WebResource()
            {
                Name = webresourceResult[0].GetAttributeValue<string>("name"),
                DisplayName = webresourceResult[0].GetAttributeValue<string>("displayname"),
                Id = webresourceResult[0].GetAttributeValue<Guid>("webresourceid")
            };
        }

        private WebResource CreateWebResource(string fileName, OrganizationService orgService, string filePath)
        {
            var createWebresoruce = new CreateWebResourceWindow(fileName);
            createWebresoruce.ShowDialog();

            if (createWebresoruce.CreatedWebResource == null)
                return null;

            var createdWebresource = createWebresoruce.CreatedWebResource;
            createdWebresource.Content = GetEncodedFileContents(filePath);
            createdWebresource.Id = orgService.Create(createdWebresource);
            return createdWebresource;
        }

        private void UpdateWebResource(OrganizationService orgService, WebResource choosenWebresource, string selectedFile)
        {
            choosenWebresource.Content = GetEncodedFileContents(selectedFile);
            var updateRequest = new UpdateRequest
            {
                Target = choosenWebresource
            };
            orgService.Execute(updateRequest);
        }

        private void PublishWebResources(OrganizationService orgService, List<WebResource> toBePublishedWebResources)
        {
            if (toBePublishedWebResources.Count < 1)
            {
                FinishingPublishing(_error, "There is no webresource to publish.");
                AddLineToOutputWindow("There is no webresource to publish.");
                return;
            }

            StartToPublish();
            var webResourcesString = "";
            foreach (var webResource in toBePublishedWebResources)
                webResourcesString = webResourcesString + string.Format("<webresource>{0}</webresource>", webResource.Id);

            var prequest = new PublishXmlRequest
            {
                ParameterXml = string.Format("<importexportxml><webresources>{0}</webresources></importexportxml>", webResourcesString)
            };
            orgService.Execute(prequest);
            FinishingPublishing(_success, null);

            var webResourcesNames = new string[toBePublishedWebResources.Count];
            for (var i = 0; i < toBePublishedWebResources.Count; i++)
            {
                webResourcesNames[i] = toBePublishedWebResources[i].Name;
            }
            AddLineToOutputWindow(string.Format("Published webresources : \n\t- {0}", string.Join("\n\t- ", webResourcesNames)));
        }

        private static EntityCollection WebresourceResult(OrganizationService orgService, string filename)
        {
            string fetchXml = string.Format(@"<fetch mapping='logical' version='1.0' >
                            <entity name='webresource' >
                                <attribute name='webresourceid' />
                                <attribute name='name' />
                                <attribute name='displayname' />
                                <filter type='and' >
                                    <condition attribute='name' operator='eq' value='{0}' />
                                </filter>
                            </entity>
                        </fetch>", filename);

            QueryBase query = new FetchExpression(fetchXml);

            var webresourceResult = orgService.RetrieveMultiple(query);
            return webresourceResult;
        }

        public string GetEncodedFileContents(string pathToFile)
        {
            var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
            byte[] binaryData = new byte[fs.Length];
            fs.Read(binaryData, 0, (int)fs.Length);
            fs.Close();
            return Convert.ToBase64String(binaryData, 0, binaryData.Length);
        }

        private void AddLineToOutputWindow(string text)
        {
            _outputWindow.AddLineToTextBox(text);
        }

        private void AddErrorLineToOutputWindow(string errorMessage)
        {
            _outputWindow.AddErrorLineToTextBox(errorMessage);
        }

        private void AddErrorToOutputWindow(string errorMessage)
        {
            _outputWindow.AddErrorText(errorMessage);
        }

        private void SetConnectionLabelText(string message, bool isSuccess)
        {
            _outputWindow.SetConnectionLabelText(message, isSuccess);
        }

        public void StartToCreateAndUpdate()
        {
            _outputWindow.StartUpdating();
        }

        public void FinishingCreateAndUpdate(bool isSuccess)
        {
            _outputWindow.FinishUpdating(isSuccess);
        }

        public void StartToPublish()
        {
            _outputWindow.StartPublishing();
        }

        public void FinishingPublishing(bool isSuccess, string text)
        {
            _outputWindow.FinishPublishing(isSuccess, text);
        }

        public void StartGettingWebresources()
        {
            _outputWindow.StartGettingWebresources();
        }

        public void FinishGettingWebresources(bool isSuccess)
        {
            _outputWindow.FinishGettingWebresources(isSuccess);
        }

        public void StartCreatingWebresources()
        {
            _outputWindow.StartCreating();
        }

        public void FinishCreatingWebresources(bool isSuccess)
        {
            _outputWindow.FinishCreating(isSuccess);
        }

        public void StartUpdatingWebresources()
        {
            _outputWindow.StartUpdating();
        }

        public void FinishUpdatingWebresources(bool isSuccess)
        {
            _outputWindow.FinishUpdating(isSuccess);
        }
    }
}