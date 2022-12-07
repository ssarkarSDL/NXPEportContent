using NXP3_ReadInputFiles.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NXP3_ReadInputFiles
{
    class Program
    {
        static void Main(string[] args)
        {
            const string userName = "SDL_Soumava";
            const string passWord = "Qwerty1@345";

            Uri serviceUrl = new Uri(@"https://nxp001.sdlproducts.com/ISHWS/"); // requires ending '/' character
            Console.WriteLine("Starting Console application for user: " + userName.ToString());
            Console.WriteLine("Autenticating the user on the specified environment...");

            InfoShareWSHelper infoShareWSHelper = new InfoShareWSHelper(serviceUrl)
            {
                Username = userName,
                Password = passWord
            };

            infoShareWSHelper.Resolve();
            //Issue a token. In other words authenticate
            infoShareWSHelper.IssueToken();

            Console.WriteLine("User " + userName.ToString() + " successfully autenticated on the specified environment.");

            try
            {
                Console.WriteLine("Starting Folder class...");
                DownloadContent.Run(infoShareWSHelper);
                Console.WriteLine("Ended DocumentObjFind_Topic...");
                Console.WriteLine("Press enter to continue...");
                Console.ReadLine();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                Console.ReadLine();
            }
        }
    }
}
