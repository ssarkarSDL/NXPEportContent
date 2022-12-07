using CsvHelper;
using NXP3_ReadInputFiles.Helper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;


namespace NXP3_ReadInputFiles
{
    class DownloadContent
    {
        public static string csv_Repository_to_LocalFolder_Mapping_localFilePath = @"C:\temp\doc_logs\outputFiles\TDRepo\_Mapping\Mapping_List.csv";

        public static async Task Run(InfoShareWSHelper _WSHelper)
        {
            try
            {
                string[] filePaths = readFilesFromFolder();
                FileStream fs = File.Create(csv_Repository_to_LocalFolder_Mapping_localFilePath);
                var newLine = string.Format("FTITLE\tObject GUID\tVersion\tLanguage\tType\tRepository Path\tLocal Path");

                using (var sr = new StreamWriter(fs))
                {
                    await sr.WriteLineAsync(newLine);
                    sr.Close();
                    sr.Dispose();
                }
                //string[] fileNames = File.ReadAllText(@"C:\temp\doc_logs\outputFiles\0001_CSV\Master_InputFileList.csv").Split('\n');
                int count = 0;
                foreach (string filePath in filePaths)
                {
                    Console.WriteLine("fileName: " + filePath);
                    //if (count > 0)
                    //{
                    string filename = Path.GetFileName(filePath);
                    //string resdown = DownloadFile(@"ftp://ftp.sct.sdl.com/Upload/NXP/inputFiles_prod", filename, "Freescale", "0pm6C7814PQtB4mp", @"C:\temp\doc_logs\inputFiles_FtpFiles\");
                    //string filePath = @"C:\temp\doc_logs\inputFiles_FtpFiles\" + filename.Trim();
                    string objType = filename.Split('_')[0];
                    List<string> logicalIDs = getLogicalIdFromFile(filePath);
                    if (logicalIDs.Count > 0)
                    {
                        foreach (string logicalID in logicalIDs)
                        {
                            if (!String.IsNullOrEmpty(logicalID))
                            {
                                string metaDataofObject = retrieveMetadata(logicalID, _WSHelper);
                                if (!String.IsNullOrEmpty(metaDataofObject))
                                {
                                    getContent(metaDataofObject, _WSHelper, objType);
                                }
                            }
                        }
                    }

                    //    File.Delete(filePath);
                    //}
                    //count++;
                }
            }
            catch(Exception e)
            {

            }
            
        }

        public static string DownloadFile(string FtpUrl, string FileNameToDownload, string userName, string password, string tempDirPath)
        {
            string ResponseDescription = "";
            string PureFileName = new FileInfo(FileNameToDownload.Trim()).Name;
            string DownloadedFilePath = tempDirPath + "/" + PureFileName;
            string downloadUrl = String.Format("{0}/{1}", FtpUrl, FileNameToDownload);
            FtpWebRequest req = (FtpWebRequest)FtpWebRequest.Create(downloadUrl);
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.Credentials = new NetworkCredential(userName, password);
            req.UseBinary = true;
            req.Proxy = null;
            try
            {
                FtpWebResponse response = (FtpWebResponse)req.GetResponse();
                Stream stream = response.GetResponseStream();
                byte[] buffer = new byte[2048];
                FileStream fs = new FileStream(DownloadedFilePath, FileMode.Create);
                int ReadCount = stream.Read(buffer, 0, buffer.Length);
                while (ReadCount > 0)
                {
                    fs.Write(buffer, 0, ReadCount);
                    ReadCount = stream.Read(buffer, 0, buffer.Length);
                }
                ResponseDescription = response.StatusDescription;
                fs.Close();
                stream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return ResponseDescription;
        }


        public static string[] readFilesFromFolder()
        {
            string[] filePaths = Directory.GetFiles(@"C:\temp\doc_logs\inputFiles_prod\");
            return filePaths;

        }
        public static List<string> getLogicalIdFromFile( string s)
        {
            string text = string.Empty;
            using (var streamReader = new StreamReader(s, Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
            }
            XmlDocument xDoc = new XmlDocument();
            xDoc.LoadXml(text);
            XmlElement root = xDoc.DocumentElement;
            XmlNodeList nodes = root.SelectNodes("ishobject");
            string ishlogicalref = string.Empty;
            List<string> ishGuid = new List<string>();
            if (nodes != null)
            {
                //int count = 0;
                foreach (XmlElement subFolderNode in nodes)
                {
                    ishlogicalref = subFolderNode.Attributes["ishlogicalref"].InnerText.ToString();
                    ishGuid.Add(subFolderNode.Attributes["ishref"].InnerText.ToString());
                    //count++;
                }
            }
            return ishGuid;
          //  return ishlogicalref;

        }
        public static string retrieveMetadata(string ishLogicalID, InfoShareWSHelper _WSHelper)
        {
            var objectClient25 = _WSHelper.GetDocumentObj25Channel();
            string[] ids = new string[1];
            ids[0] = ishLogicalID;
            String xmlMetadataFilter = "<ishfields>" +
"</ishfields>";
            string xmlRequestedMetadata = "<ishfields>" +
    "<ishfield name='FTITLE' level='logical'/>" +
    "<ishfield name='VERSION' level='version'/>" +
    "<ishfield name='FAUTHOR' level='lng'/>" +
    "<ishfield name='FSTATUS' level='lng'/>" +
    "<ishfield name='DOC-LANGUAGE' level='lng'/>" +
    "<ishfield name='FRESOLUTION' level='lng'/>"+
"</ishfields>";

            string requestedMetadata = objectClient25.RetrieveMetadata(ids, DocumentObj25ServiceReference.StatusFilter.ISHNoStatusFilter, null, xmlRequestedMetadata);
           // Console.WriteLine("requestedMetadata: "+requestedMetadata);
            return requestedMetadata;
        }

        public async static void getContent(string metadatInfo, InfoShareWSHelper _WSHelper, string objType)
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.LoadXml(metadatInfo);
            XmlElement root = xDoc.DocumentElement;
            XmlNodeList nodes = root.SelectNodes("ishobject");
           // string ishlogicalref = string.Empty;
            string ishGuid = string.Empty;
            string FTITLE = string.Empty;
            string FRESOLUTION = string.Empty;
            string ishversionref = string.Empty;
            string ishlngref = string.Empty;
            var objectClient25 = _WSHelper.GetDocumentObj25Channel();
            try
            {
                if (nodes != null)
                {
                    foreach (XmlElement node in nodes)
                    {
                        //ishlogicalref = node.Attributes["ishlogicalref"].InnerText.ToString();
                        ishGuid = node.Attributes["ishref"].InnerText.ToString();
                        //XmlNode TempNode1 = node.CloneNode(true);
                        //XmlDocument xDoc1 = new XmlDocument();
                        //xDoc1.LoadXml(TempNode1.InnerText);
                        //foreach (XmlNode subChildNode in xDoc1.SelectNodes("//ishfield"))
                        //{
                        //    if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("version"))
                        //    {
                        //        ishversionref = subChildNode.InnerText.ToString();
                        //    }
                        //    if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("doc-language"))
                        //    {
                        //        ishlngref = subChildNode.InnerText.ToString();
                        //    }
                        //    if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("ftitle"))
                        //    {
                        //        FTITLE = subChildNode.InnerText.ToString();
                        //    }
                        //    if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("fresolution"))
                        //    {
                        //        FRESOLUTION = subChildNode.InnerText.ToString();
                        //    }
                        //}

                        foreach (XmlElement childNode in node.ChildNodes)
                        {
                            foreach (XmlElement subChildNode in childNode.ChildNodes)
                            {
                                if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("version"))
                                {
                                    ishversionref = subChildNode.InnerText.ToString();
                                }
                                if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("doc-language"))
                                {
                                    ishlngref = subChildNode.InnerText.ToString();
                                }
                                if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("ftitle"))
                                {
                                    FTITLE = subChildNode.InnerText.ToString();
                                }
                                if (subChildNode.Attributes["name"].InnerText.ToString().ToLower().Equals("fresolution"))
                                {
                                    FRESOLUTION = subChildNode.InnerText.ToString();
                                }
                            }
                            string[] objDocsFolderLocation = new string[] { };
                            long[] objDocsFolderID = new long[] { };
                            string documentContent = string.Empty;
                            if (!String.IsNullOrEmpty(ishGuid) && !String.IsNullOrEmpty(ishversionref) && !String.IsNullOrEmpty(ishlngref))
                            {
                                objectClient25.GetObject(ishGuid, ref ishversionref, out documentContent, ishlngref, FRESOLUTION, "", "");
                                objectClient25.FolderLocation(out objDocsFolderLocation, out objDocsFolderID, ishGuid);
                                //string s=objectClient25.FolderLocations(new string[] {ishGuid});
                                string folderPath = string.Join("/", objDocsFolderLocation);
                                //folderPath = "~/" + folderPath + "/";
                                await writeObjContentToFile(documentContent, ishGuid, ishversionref, ishlngref, objType, FTITLE, folderPath, FRESOLUTION);

                            }
                        }

                    }
                }

            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async static Task writeObjContentToFile(string content, string ishGuid, string ishversionref, string ishlngref, string objType, string FTITLE, string folderPath, string FRESOLUTION)
        {
            try 
            {
                FTITLE = FTITLE.Replace("\n", "").Replace("\r", "").Replace("\t", " ").Replace(@"	", " ");
                FTITLE = string.Join(" ", Regex.Split(FTITLE, @"(?:\r\n|\n|\r)"));
                //if (FTITLE.Contains("."))
                //{
                //    FTITLE = FTITLE.Split('.')[0];
                //}
                //if (FTITLE.Length > 30)
                //{
                //    FTITLE = FTITLE.Substring(0, 29);
                //}
                //FTITLE = FTITLE.Replace('.', '_').Replace('<', '_').Replace('>', '_').Replace('[', '_').Replace(']', '_').Replace(':', '_').Replace('~', '_').Replace('!', '_').Replace('@', '_').Replace('#', '_').Replace('$', '_').Replace('%', '_').Replace('^', '_').Replace('&', '_').Replace('*', '_').Replace('-', '_').Replace('\\', '_').Replace('/', '_').Replace('=', '_').Replace('(', '_').Replace(')', '_');
                //regex
                //FTITLE=Regex.Replace(FTITLE,@"[^a-zA-Z0-9 ]","_");
                //folderpath
                if (folderPath.ToLowerInvariant().StartsWith("condition management") || folderPath.ToLowerInvariant().StartsWith("editor templates") || folderPath.ToLowerInvariant().StartsWith("publishing") || folderPath.ToLowerInvariant().StartsWith("synchronizer"))
                {
                    folderPath = "System/"+folderPath;
                }
                else
                {
                    folderPath = "NXP_Prod/" + folderPath;
                }
                string baseOutputFolderPath = @"C:\temp\doc_logs\outputFiles\TDRepo\";
                string ishResolution = FRESOLUTION;
                string subFolderName = ishGuid.Split('-')[1].Substring(0, 3);
                int subfolderVersions = 0;
                string[] existingFiles = new string[] { };
                string tempFTITLE1 = string.Empty;
                if (FTITLE.Length > 30)
                {
                    tempFTITLE1 = FTITLE.Substring(0, 29);
                }
                else
                {
                    tempFTITLE1 = FTITLE;
                }

                string filename =  Regex.Replace(tempFTITLE1, @"[^a-zA-Z0-9 .]", "_") + "=" + ishGuid + "=" + ishversionref + "=" + ishlngref + "=" + ishResolution;
                if (Directory.Exists(baseOutputFolderPath + subFolderName+ @"\"))
                {
                    existingFiles = Directory.GetFiles(baseOutputFolderPath + subFolderName);
                    int fileCount = existingFiles.Length;
                    subfolderVersions = (fileCount / 10000);
                    if (subfolderVersions > 0)
                    {
                        subfolderVersions++;
                        subFolderName = subFolderName + "_" + subfolderVersions;
                        if (!Directory.Exists(baseOutputFolderPath + subFolderName + @"\"))
                        {
                            DirectoryInfo tempFolder = new DirectoryInfo(@"C:\temp\doc_logs\outputFiles\TDRepo\");
                            DirectoryInfo subFolder = tempFolder.CreateSubdirectory(subFolderName);
                        }
                            

                    }

                }
                else
                {
                    //subFolderName = subFolderName;
                    DirectoryInfo tempFolder = new DirectoryInfo(@"C:\temp\doc_logs\outputFiles\TDRepo\");
                    DirectoryInfo subFolder = tempFolder.CreateSubdirectory(subFolderName);
                }
                string localFilePath = string.Empty;
                
                if (objType == "image" || objType=="other")
                {
                    string fileType = string.Empty;
                    XmlDocument xDoc = new XmlDocument();
                    xDoc.LoadXml(content);
                    XmlElement root = xDoc.DocumentElement;
                    XmlNodeList nodes = root.SelectNodes("//ishdata");
                    
                    if(nodes!=null)
                    {
                        foreach (XmlElement subChildNode in nodes)
                        {
                            //fileType = childNode.Attributes["fileextension"].InnerText.ToString();

                            //foreach (XmlElement subChildNode in childNode.ChildNodes)
                            //{
                                fileType = subChildNode.Attributes["fileextension"].InnerText.ToString();
                                string imgContent = subChildNode.InnerText.ToString();
                                string tempFTITLE = string.Empty;
                                if (FTITLE.Length > 30)
                                {
                                    tempFTITLE = FTITLE.Substring(0, 29);
                                }
                                else
                                {
                                    tempFTITLE = FTITLE;
                                }
                                filename = Regex.Replace(tempFTITLE1, @"[^a-zA-Z0-9 .]", "_") + "=" + ishGuid + "=" + ishversionref + "=" + ishlngref + "=" + ishResolution + "."+ fileType;
                                localFilePath = baseOutputFolderPath + subFolderName + @"\" + filename;
                                //byte[] data = System.Convert.FromBase64String(imgContent);
                                File.WriteAllBytes(localFilePath, Convert.FromBase64String(imgContent));
                                Console.WriteLine("localBinaryFilePath: " + localFilePath);
                       //     }

                        }
                    }
                }
                else
                {
                    
                    //localFilePath = baseOutputFolderPath + subFolderName + @"\" + filename;
                    //FileStream fs = File.Create(localFilePath);
                    string fileType = string.Empty;
                    XmlDocument xDoc = new XmlDocument();
                    xDoc.LoadXml(content);
                    XmlElement root = xDoc.DocumentElement;
                    XmlNodeList nodes = root.SelectNodes("//ishdata");

                    if (nodes != null)
                    {
                        foreach (XmlElement subChildNode in nodes)
                        {
                            //foreach (XmlElement subChildNode in childNode.ChildNodes)
                            //{
                                fileType = subChildNode.Attributes["fileextension"].InnerText.ToString();

                                string objContent = subChildNode.InnerText.ToString();
                                localFilePath = baseOutputFolderPath + subFolderName + @"\" + filename+"."+ fileType;
                                File.WriteAllBytes(localFilePath, Convert.FromBase64String(objContent));
                                Console.WriteLine("localBinaryFilePath: " + localFilePath);

                            //}
                        }
                    }
                    
                }
                string reportLocalfilepath = localFilePath.Replace(@"C:\temp\doc_logs\outputFiles\", @".\");
                //reportLocalfilepath=@"c:"+ reportLocalfilepath
                //related to requirement 4
                var newContent = string.Format("{0}{1}{2}{3}{4}{5}{6}", "\""+FTITLE+"\"\t", "\""+ishGuid+ "\"\t", "\""+ishversionref+ "\"\t", "\""+ishlngref+ "\"\t", "\"" + objType+ "\"\t", "\""+folderPath+ "\"\t", "\""+ reportLocalfilepath + "\"");
                if (File.Exists(csv_Repository_to_LocalFolder_Mapping_localFilePath))
                {
                    using (StreamWriter sw = File.AppendText(csv_Repository_to_LocalFolder_Mapping_localFilePath))
                    {
                        sw.WriteLine(newContent);
                        sw.Close();
                        sw.Dispose();
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        
        }

    }
  
}
