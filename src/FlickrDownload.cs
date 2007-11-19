/*****************************************************************************/
/*  FlickrDownload                                                           */
/*  Copyright (C) 2007 Brian Masney <masneyb@gftp.org>                       */
/*                                                                           */
/*  This program is free software; you can redistribute it and/or modify     */
/*  it under the terms of the GNU General Public License as published by     */
/*  the Free Software Foundation; either version 3 of the License, or        */
/*  (at your option) any later version.                                      */
/*                                                                           */
/*  This program is distributed in the hope that it will be useful,          */
/*  but WITHOUT ANY WARRANTY; without even the implied warranty of           */
/*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the            */
/*  GNU General Public License for more details.                             */
/*                                                                           */
/*  You should have received a copy of the GNU General Public License        */
/*  along with this program. If not, see <http://www.gnu.org/licenses/>.     */
/*****************************************************************************/

using System.Configuration;

namespace org.gftp
{

static class FlickrDownload
  {
    static string xsltBasePath;

    static void WriteProgramBanner()
      {
        System.Console.WriteLine("FlickrDownload 0.1 Copyright(C) 2007 Brian Masney <masneyb@gftp.org>.");
        System.Console.WriteLine("If you have any questions, comments, or suggestions about this program, please");
        System.Console.WriteLine("feel free to email them to me. You can always find out the latest news about");
        System.Console.WriteLine("FlickrDownload from my website at http://www.gftp.org/FlickrDownload/");
        System.Console.WriteLine("");
        System.Console.WriteLine("FlickrDownload comes with ABSOLUTELY NO WARRANTY; for details, see the COPYING");
        System.Console.WriteLine("file. This is free software, and you are welcome to redistribute it under");
        System.Console.WriteLine("certain conditions; for details, see the COPYING file.");
        System.Console.WriteLine("");
      }

    static void usage()
      {
        System.Console.WriteLine("FlickrDownload <username> [output directory]");
        System.Console.WriteLine("");
        System.Console.WriteLine("Note: You may have to enclose your username in quotes (\") if it has spaces.");
      }

    static void WriteAuthToken(string authToken)
      {
        Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None); 
        config.AppSettings.Settings.Add ("flickrAuthToken", authToken);
        config.Save();
      }

    static FlickrNet.Flickr initFlickrSession()
      {
        FlickrNet.Flickr flickr = new FlickrNet.Flickr ("16c1a6a31f28e670500d02f6b13935b1", "0fa4d39da5eab415");

        string authToken = System.Configuration.ConfigurationManager.AppSettings["flickrAuthToken"];
        while (authToken == null || authToken.Length == 0)
          {
            string Frob = flickr.AuthGetFrob();
            string url = flickr.AuthCalcUrl(Frob, FlickrNet.AuthLevel.Read);

            System.Console.WriteLine ("You must authenticate this application with Flickr. Please pull up the following");
            System.Console.WriteLine ("URL in your web browser:");
            System.Console.WriteLine ("");
            System.Console.WriteLine("\t" + url);
            System.Console.WriteLine ("");
            System.Console.WriteLine ("Press enter once you have authenticated the application with Flickr.");
        
            System.Console.ReadLine();

            try
              {
                FlickrNet.Auth auth = flickr.AuthGetToken(Frob);
                if (auth != null) 
                  {
                    authToken = auth.Token;
                    WriteAuthToken(authToken);
                  }
              }
            catch (FlickrNet.FlickrApiException e)
              {
                System.Console.WriteLine ("");
                System.Console.WriteLine("Error receiving the authentication token: " + e);
                System.Console.WriteLine ("");
              }
          }
       
        flickr.AuthToken = authToken;

        return flickr;
      }

    static void addXmlTextNode (System.Xml.XmlDocument xmlDoc, System.Xml.XmlElement parent, string name, string value)
      {
        System.Xml.XmlElement element = xmlDoc.CreateElement (name);
        parent.AppendChild (element);

        if (value != "")
          {
            System.Xml.XmlText text = xmlDoc.CreateTextNode (value);
            element.AppendChild (text);
          }
      }

    static void PerformXsltTransformation(string xsltSetting, string xmlFile, string outputFile)
      {
        string xsltFile = System.Configuration.ConfigurationManager.AppSettings[xsltSetting];
        if (xsltFile == null || xsltFile == "")
          {
            System.Console.WriteLine ("The setting " + xsltSetting + " is not set in the application config file.");
            System.Environment.Exit (1);
          }

        xsltFile = System.IO.Path.Combine (xsltBasePath, xsltFile);

        System.Console.WriteLine ("Performing XSLT transformation:");
        System.Console.WriteLine ("\tCreating " + outputFile + " based on " + xmlFile + " using " + xsltFile);

        try
          {
            System.Xml.XPath.XPathDocument xPathDoc = new System.Xml.XPath.XPathDocument (xmlFile);

            System.Xml.Xsl.XslTransform xsltTrans = new System.Xml.Xsl.XslTransform ();
            xsltTrans.Load (xsltFile);
            
            MultiOutput.MultiXmlTextWriter outputXHtml = new MultiOutput.MultiXmlTextWriter (outputFile, null);

            xsltTrans.Transform (xPathDoc, null, outputXHtml);        

            outputXHtml.Close() ;
          }
        catch (System.Exception e)
          {
            System.Console.WriteLine ("Error performing the XSLT transformation: " + e.Message);
          }
      }

    static void DownloadPhotoSet(FlickrNet.Flickr flickr, FlickrNet.Photoset set, string outputPath)
      {
        string setDirectory = System.IO.Path.Combine (outputPath, set.PhotosetId);
        System.IO.Directory.CreateDirectory (setDirectory);

        System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument ();
        System.Xml.XmlNode xmlNode = xmlDoc.CreateNode (System.Xml.XmlNodeType.XmlDeclaration, "", "");
        xmlDoc.AppendChild (xmlNode);

        System.Xml.XmlElement setTopLevelXmlNode = xmlDoc.CreateElement ("set");
        xmlDoc.AppendChild (setTopLevelXmlNode);

        addXmlTextNode (xmlDoc, setTopLevelXmlNode, "title", set.Title);
        addXmlTextNode (xmlDoc, setTopLevelXmlNode, "description", set.Description);

        foreach (FlickrNet.Photo photo in flickr.PhotosetsGetPhotos (set.PhotosetId).PhotoCollection)
          {
            FlickrNet.PhotoInfo pi = flickr.PhotosGetInfo (photo.PhotoId);
            
            string thumbUrl = photo.ThumbnailUrl;
            string thumbFile = photo.PhotoId + "_thumb.jpg";
            DownloadPicture (flickr, thumbUrl, System.IO.Path.Combine (setDirectory, thumbFile));
            
            string medUrl = photo.MediumUrl;
            string medFile = photo.PhotoId + "_med.jpg";
            DownloadPicture (flickr, medUrl, System.IO.Path.Combine (setDirectory, medFile));
            
            string origUrl = photo.OriginalUrl;
            string origFile = photo.PhotoId + "_orig.jpg";
            DownloadPicture (flickr, origUrl, System.IO.Path.Combine (setDirectory, origFile));

            System.Xml.XmlElement photoXmlNode = xmlDoc.CreateElement ("photo");
            setTopLevelXmlNode.AppendChild (photoXmlNode);

            addXmlTextNode (xmlDoc, photoXmlNode, "id", photo.PhotoId);
            addXmlTextNode (xmlDoc, photoXmlNode, "title", photo.Title);
            addXmlTextNode (xmlDoc, photoXmlNode, "description", pi.Description);
            addXmlTextNode (xmlDoc, photoXmlNode, "dateTaken", photo.DateTaken.ToString());
            addXmlTextNode (xmlDoc, photoXmlNode, "tags", photo.CleanTags);
            
            try
              {
                FlickrNet.PhotoPermissions privacy = flickr.PhotosGetPerms (photo.PhotoId);
                
                if (privacy.IsPublic)
                  addXmlTextNode (xmlDoc, photoXmlNode, "privacy", "public");
                else if (privacy.IsFamily && privacy.IsFriend)
                  addXmlTextNode (xmlDoc, photoXmlNode, "privacy", "friend/family");
                else if (privacy.IsFamily)
                  addXmlTextNode (xmlDoc, photoXmlNode, "privacy", "family");
                else if (privacy.IsFriend)
                  addXmlTextNode (xmlDoc, photoXmlNode, "privacy", "friend");
                else
                  addXmlTextNode (xmlDoc, photoXmlNode, "privacy", "private");
              }
            catch (FlickrNet.FlickrApiException)
              {
              }

            addXmlTextNode (xmlDoc, photoXmlNode, "originalFile", origFile);
            addXmlTextNode (xmlDoc, photoXmlNode, "mediumFile", medFile);
            addXmlTextNode (xmlDoc, photoXmlNode, "thumbnailFile", thumbFile);
          }

        string xmlFile = System.IO.Path.Combine (setDirectory, "photos.xml");
        xmlDoc.Save (xmlFile);

        /* FIXME - This current fails because the document() function in XSLT
           is not supported. */
        string htmlFile = System.IO.Path.Combine (setDirectory, "index.html");
        PerformXsltTransformation("setXsltFile", xmlFile, htmlFile);
      }

    static void DownloadPicture (FlickrNet.Flickr flickr, string url, string fileName)
      {
        if (System.IO.File.Exists (fileName))
          {
            System.Console.WriteLine ("Skipping file " + fileName + " since it has already been downloaded.");
            return;
          }
                  
        System.Console.WriteLine ("Downloading file " + fileName);
                
        System.IO.Stream input = flickr.DownloadPicture (url);
        System.IO.FileStream output = System.IO.File.Create (fileName);
                
        int numBytes;
        const int size = 8192;
        byte[] bytes = new byte[size];
        
        while((numBytes = input.Read (bytes, 0, size)) > 0)
          output.Write(bytes, 0, numBytes);
          
        input.Close ();
        output.Close ();
      }

    static void CopyPhotosDotCSS (string destFile)
      {
        string sourceFile = System.Configuration.ConfigurationManager.AppSettings["photosCssFile"];
        if (sourceFile == null || sourceFile == "")
          {
            System.Console.WriteLine ("The setting photosCssFile is not set in the application config file.");
            System.Environment.Exit (1);
          }

        sourceFile = System.IO.Path.Combine (xsltBasePath, sourceFile);
        System.Console.WriteLine ("Copying " + sourceFile + " to " + destFile);
        System.IO.File.Copy (sourceFile, destFile);
      }

    static int Main (string[] argv)
      {
        WriteProgramBanner();

        if (argv.Length < 1 || argv.Length > 2)
          {
            usage();
            return 1;
          }

        string outputPath;
        if (argv.Length == 2)
          outputPath = argv[1];
        else
          outputPath = ".";

        xsltBasePath = System.IO.Path.Combine (System.IO.Path.GetDirectoryName (System.Reflection.Assembly.GetExecutingAssembly().Location), "..");

        FlickrNet.Flickr flickr = initFlickrSession();

        System.Console.WriteLine ("Downloading photo set information for user '" + argv[0] + "'");
        FlickrNet.Photosets sets;
        try
          {
            sets = flickr.PhotosetsGetList (flickr.PeopleFindByUsername (argv[0]).UserId);
          }
        catch (FlickrNet.FlickrException ex)
          {
            System.Console.WriteLine ("Error retrieving photos for user " + argv[0] + ": " + ex.Message);
            return 1;
          }
          
        System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument ();
        System.Xml.XmlNode xmlNode = xmlDoc.CreateNode (System.Xml.XmlNodeType.XmlDeclaration, "", "");
        xmlDoc.AppendChild (xmlNode);

        System.Xml.XmlElement setTopLevelXmlNode = xmlDoc.CreateElement ("sets");
        xmlDoc.AppendChild (setTopLevelXmlNode);

        foreach (FlickrNet.Photoset set in sets.PhotosetCollection)
          {
            System.Xml.XmlElement setXmlNode = xmlDoc.CreateElement ("set");
            setTopLevelXmlNode.AppendChild (setXmlNode);
            
            addXmlTextNode (xmlDoc, setXmlNode, "title", set.Title);
            addXmlTextNode (xmlDoc, setXmlNode, "directory", set.PhotosetId);

            string primaryPhoto = set.PrimaryPhotoId + "_thumb.jpg";
            addXmlTextNode (xmlDoc, setXmlNode, "thumbnailFile", set.PhotosetId + "/" + primaryPhoto);

            DownloadPhotoSet (flickr, set, outputPath);
          }
          
        string xmlFile = System.IO.Path.Combine (outputPath, "sets.xml");
        xmlDoc.Save (xmlFile);

        string htmlFile = System.IO.Path.Combine (outputPath, "index.html");
        PerformXsltTransformation("allSetsXsltFile", xmlFile, htmlFile);

        CopyPhotosDotCSS (System.IO.Path.Combine (outputPath, "photos.css"));

        return 0;
      }
  }

}
