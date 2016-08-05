using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using System.Linq;

namespace GetDataFromServerAndSaveInFile
{
    public class Program
    {
        private static bool german = true;
        private static bool blog = true;
        private static Dictionary<string, string> idWithPathsPagesGerman = new Dictionary<string, string>();
        private static Dictionary<string, string> idWithPathsPagesEnglish = new Dictionary<string, string>();
        private static Dictionary<string, string> idWithPathsData = new Dictionary<string, string>();
        //id mit englisch und deutsch titel
        private static Dictionary<string, string[]> idWithPathsGermanEnglish = new Dictionary<string, string[]>();
        private static Dictionary<string, string[]> idWithPathsBlogs = new Dictionary<string, string[]>();
        private static Dictionary<string, string> idWithPathsDevBlogs = new Dictionary<string, string>();
        private static string[] filesToIgnore;
        private static string currentPathBlog;
        private static string currentPathPage;

        public static void Main(string[] args)
        {
            using (var conn = new SqlConnection())
            {
                conn.ConnectionString = ConfigurationManager.AppSettings["ConnectString"];
                conn.Open();

                var pageGerman = @"SELECT p.id,p.title,p.menutitle,p.urlTitle,p.friendlyUrl,p.description,pp.placeHolderId,pp.content FROM Composite_Data_Types_IPage_Published_de_AT p inner join Composite_Data_Types_IPagePlaceholderContent_Published_de_AT pp on pp.pageid = p.id order by p.id, pp.placeholderid desc;";
                var urlGerman = @"with pages as (select s.id, 0 as level, cast(pn.UrlTitle as nvarchar(max)) as urltitle from [dbo].[Composite_Data_Types_IPageStructure_Published] s inner join [Composite_Data_Types_IPage_Published_de_AT] pn on pn.id = s.id where parentid = '00000000-0000-0000-0000-000000000000'union all select  p1.id, p0.level + 1, p0.urltitle + '/' + cast(pn.UrlTitle as nvarchar(max)) as urltitle from Composite_Data_Types_IPageStructure_Published p1 inner join pages p0 on p0.id = p1.parentid inner join Composite_Data_Types_IPage_Published_de_AT pn on pn.id = p1.id) select * from pages";

                var pageEnglish = @"SELECT p.id,p.title,p.menutitle,p.urlTitle,p.friendlyUrl,p.description,pp.placeHolderId,pp.content FROM Composite_Data_Types_IPage_Published_en_US p inner join Composite_Data_Types_IPagePlaceholderContent_Published_en_US pp on pp.pageid = p.id order by p.id, pp.placeholderid desc";
                var urlEnglish = @"with pages as (select s.id, 0 as level, cast(pn.UrlTitle as nvarchar(max)) as urltitle from [dbo].[Composite_Data_Types_IPageStructure_Published] s inner join Composite_Data_Types_IPage_Published_en_US pn on pn.id = s.id where parentid = '00000000-0000-0000-0000-000000000000' union all select  p1.id, p0.level + 1, p0.urltitle + '/' + cast(pn.UrlTitle as nvarchar(max)) as urltitle from Composite_Data_Types_IPageStructure_Published p1 inner join pages p0 on p0.id = p1.parentid inner join Composite_Data_Types_IPage_Published_en_US pn on pn.id = p1.id) select * from pages;";

                var mediaFile = @"select id, folderPath, fileName from Composite_Data_Types_IMediaFileData_Published;";

                var blogEntriesGerman = @"SELECT p.id,p.TitleUrl,p.title,p.image,p.imagesource,p.Teaser,p.tags,p.date,p.Author,p.Content,p.SourceCultureName,pp.name FROM Composite_Community_Blog_Entries_de_AT p inner join Composite_Community_Blog_Authors_Published pp on p.author = pp.id;";
                var blogEntriesEnglish = @"SELECT p.id,p.TitleUrl,p.title,p.image,p.imagesource,p.Teaser,p.tags,p.devblog,p.date,p.Author,p.Content,p.SourceCultureName,pp.name FROM Composite_Community_Blog_Entries_en_US p inner join Composite_Community_Blog_Authors_Published pp on p.author = pp.id;";

                using (var dataEnglish = new SqlDataAdapter(pageEnglish, conn))
                using (var urlDataEnglish = new SqlDataAdapter(urlEnglish, conn))
                using (var dataGerman = new SqlDataAdapter(pageGerman, conn))
                using (var urlDataGerman = new SqlDataAdapter(urlGerman, conn))
                using (var urlDataMediaFile = new SqlDataAdapter(mediaFile, conn))
                using (var dataBlogEntriesGerman = new SqlDataAdapter(blogEntriesGerman, conn))
                using (var dataBlogEntriesEnglish = new SqlDataAdapter(blogEntriesEnglish, conn))
                {
                    using (var dataTableEnglish = new DataTable())
                    using (var urlDataTableEnglish = new DataTable())
                    using (var dataTableGerman = new DataTable())
                    using (var urlDataTableGerman = new DataTable())
                    using (var urlDataTableMediaFile = new DataTable())
                    using (var dataTableBlogEntriesGerman = new DataTable())
                    using (var dataTableBlogEntriesEnglish = new DataTable())
                    {
                        dataEnglish.Fill(dataTableEnglish);
                        urlDataEnglish.Fill(urlDataTableEnglish);
                        dataGerman.Fill(dataTableGerman);
                        urlDataGerman.Fill(urlDataTableGerman);
                        urlDataMediaFile.Fill(urlDataTableMediaFile);
                        dataBlogEntriesGerman.Fill(dataTableBlogEntriesGerman);
                        dataBlogEntriesEnglish.Fill(dataTableBlogEntriesEnglish);


                        //Just fills the dictionarys 
                        FillDictionaryHtml(dataTableGerman, urlDataTableGerman, idWithPathsPagesGerman);
                        FillDictionaryBlog(dataTableBlogEntriesGerman);

                        german = false;

                        FillDictionaryHtml(dataTableEnglish, urlDataTableEnglish, idWithPathsPagesEnglish);
                        FillDictionaryBlog(dataTableBlogEntriesEnglish);

                        german = true;

                        CheckDirectory(ConfigurationManager.AppSettings["RootUrl"]);

                        AddFilesToIgnore();

                        //Download all documents
                        DownloadData(urlDataTableMediaFile);

                        //Make blogs
                        MakeBlog(dataTableBlogEntriesGerman);

                        german = false;

                        MakeBlog(dataTableBlogEntriesEnglish);

                        german = true;
                        blog = false;

                        //Make html
                        MakeHtml(dataTableGerman, urlDataTableGerman, "DeRootUrl");

                        german = false;

                        MakeHtml(dataTableEnglish, urlDataTableEnglish, "EnRootUrl");
                    }
                }
            }
            //Console.ReadKey();
        }

        private static void AddFilesToIgnore()
        {
			filesToIgnore = new string[] { "/blog/", "/de/blog/", "/de/", "/", "/de/preis/preis/", "/pricing/pricing/", "/de/hilfe-support/kontakt/", "/help-support/contact-us/" };
		}

        private static int CheckToIgnore(string path)
        {
            int check;

            if (path == "/home/")
                path = "/";

            if (german)
                check = Array.IndexOf(filesToIgnore, $"/de{path}");
            else
                check = Array.IndexOf(filesToIgnore, path);

            return check;
        } 

        /// <summary>
        /// Makes the current path for a blog
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="index"></param>
        /// <param name="rootUrl"></param>
        /// <returns>filename</returns>
        private static string MakePath(DataTable dt, int index, string rootUrl)
        {
            DateTime date = Convert.ToDateTime(dt.Rows[index]["date"]);
            var path = "";

            if (rootUrl == "DevRootUrl")
                path = "/devblog/";
            else
                path = "/blog/";

            var month = date.Month.ToString();
            var day = date.Day.ToString();

            if (date.Month.ToString().Length == 1)
                month = $"0{date.Month}";
            if (date.Day.ToString().Length == 1)
                day = $"0{date.Day}";

            path += $"{date.Year}/{month}/{day}";

            CheckDirectory($"{ConfigurationManager.AppSettings[rootUrl]}"); //erstellen des ordners

            currentPathBlog = $"{path}/{dt.Rows[index]["titleUrl"]}";

            return $"{date.Year}-{month}-{day}-{dt.Rows[index]["titleUrl"]}";
        }

        /// <summary>
        /// Wird nur aufgerufen um das Dicitionary zu füllen
        /// </summary>
        /// <param name="dt"></param>
        private static void FillDictionaryBlog(DataTable dt)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var id = dt.Rows[i]["id"].ToString();

                if (!german)
                {
                    if (dt.Rows[i]["devblog"].ToString() == "True")
                    {
                        MakePath(dt, i, "DevRootUrl");

                        idWithPathsDevBlogs.Add(id, currentPathBlog);
                    }
                    else
                    {
                        MakePath(dt, i, "RootUrl");

                        if (idWithPathsBlogs.ContainsKey(id))
                            idWithPathsBlogs[id][1] = currentPathBlog;
                        else
                            idWithPathsBlogs.Add(id, new string[] { "", currentPathBlog });
                    }
                }
                else
                {
                    MakePath(dt, i, "RootUrl");

                    idWithPathsBlogs.Add(id, new string[] { $"/de{currentPathBlog}", "" });
                }
            }
        }

        /// <summary>
        /// Makes the blog with header and content (same code like FillDictionaryBlog
        /// </summary>
        /// <param name="dt"></param>
        private static void MakeBlog(DataTable dt)
        {
            var url = "";
            var fileName = "";

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                url = "RootUrl";
                fileName = "";

                var id = dt.Rows[i]["id"].ToString();

                if (!german)
                {
                    if (dt.Rows[i]["devblog"].ToString() == "True")
                    {
                        fileName = MakePath(dt, i, "DevRootUrl");
                        url = "DevRootUrl";
                    }
                    else
                        fileName = MakePath(dt, i, "RootUrl");
                }
                else
                    fileName = MakePath(dt, i, "RootUrl");

                if (CheckToIgnore(currentPathBlog) <= -1)
                {
                    using (var sw = new StreamWriter($"{ConfigurationManager.AppSettings[url]}{fileName}.md"))
                    {
                        SetHeader(sw, dt, i);
                        ContentSeperatorAndSetter(sw, dt.Rows[i]["content"].ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Alle dateien werden hier runtergeladen
        /// </summary>
        /// <param name="dt"></param>
        private static void DownloadData(DataTable dt)
        {
            using (var client = new WebClient())
            {
                client.BaseAddress = ConfigurationManager.AppSettings["TimecockpitUrl"];

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var help = dt.Rows[i]["folderPath"].ToString().Split('/');
                    var folder = "";

                    for (int j = 0; j < help.Length; j++)
                    {
                        if (help[j] == "time_cockpit")
                            folder += "images/";
                        else if (help[j] != string.Empty)
                            folder += $"{help[j]}/";
                    }
                    var path = $"/content/{folder}{dt.Rows[i]["fileName"]}";

                    idWithPathsData.Add(dt.Rows[i]["id"].ToString(),path);

                    var fullPath = $"{ConfigurationManager.AppSettings["ContentRootUrl"]}{folder}";

                    CheckDirectory(fullPath);

                    fullPath = $"{fullPath}{dt.Rows[i]["fileName"]}";

                    if (!CheckFile(fullPath))
                    {
                        var data = client.DownloadData(dt.Rows[i]["id"].ToString());
                        File.WriteAllBytes(fullPath, data);
                        Console.WriteLine(i);
                    }
                }
            }
        }   

        /// <summary>
        /// Fills the dictionary for the html page
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="urlDt"></param>
        /// <param name="pages"></param>
        private static void FillDictionaryHtml(DataTable dt, DataTable urlDt, Dictionary<string,string> pages)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var foundrow = urlDt.Select($"id = '{dt.Rows[i]["id"]}'");

                var splitUrl = GetUrlTitle(foundrow[0]["urltitle"].ToString());

                var folderPath = "/";
                var fileName = "";

                if (splitUrl.Length == 1)
                    fileName = "home";
                else
                {
                    for (int j = 0; j < splitUrl.Length - 1; j++)
                    {
                        if (splitUrl[j] != string.Empty)
                            folderPath += splitUrl[j] + "/";
                    }
                    fileName = splitUrl[splitUrl.Length - 1];
                }

                if (german)
                    currentPathPage = $"/de{folderPath}{fileName}/";
                else
                    currentPathPage = $"{folderPath}{fileName}/";

                if (!pages.ContainsKey(foundrow[0]["id"].ToString()))
                    pages.Add(foundrow[0]["id"].ToString(), currentPathPage);

                var index = dt.Rows[i]["id"].ToString();

                if(german)
                {
                    if (!idWithPathsGermanEnglish.ContainsKey(index))
                        idWithPathsGermanEnglish.Add(index, new string[] { $"{currentPathPage}", "" });
                }
                else
                {
                    if (idWithPathsGermanEnglish.ContainsKey(index))
                        idWithPathsGermanEnglish[index][1] = currentPathPage;
                    else
                        idWithPathsGermanEnglish.Add(index, new string[] { "", currentPathPage });
                }
            }
        }

        /// <summary>
        /// Makes the html page 
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="urlDt"></param>
        /// <param name="rootUrl"></param>
        private static void MakeHtml(DataTable dt, DataTable urlDt, string rootUrl)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var foundrow = urlDt.Select($"id = '{dt.Rows[i]["id"]}'");

                var splitUrl = GetUrlTitle(foundrow[0]["urltitle"].ToString());

                var folderPath = "/";
                var helpPath = "";
                var fileName = "";

                if (splitUrl.Length == 1)
                    fileName = "home";
                else { 
                    for (int j = 0; j < splitUrl.Length - 1; j++)
                    {
                        if (splitUrl[j] != string.Empty)
                            folderPath += splitUrl[j] + "/";
                    }
                    fileName = splitUrl[splitUrl.Length - 1];
                }
                helpPath = $"{folderPath}{fileName}/";

                currentPathBlog = helpPath;

                if (CheckToIgnore(helpPath) <= -1)
                {
                    if (dt.Rows[i]["placeholderid"].ToString() == "content")
                    {
                        CheckDirectory($"{ConfigurationManager.AppSettings[rootUrl]}{folderPath}");

                        using (var sw = new StreamWriter($"{ConfigurationManager.AppSettings[rootUrl]}{folderPath}{fileName}.md"))
                        {
                            SetHeader(sw, dt, i);
                            ContentSeperatorAndSetter(sw, dt.Rows[i]["content"].ToString());
                        }
                    }
                    else
                    {
                        using (var sw = new StreamWriter($"{ConfigurationManager.AppSettings[rootUrl]}{folderPath}{fileName}.md", true))
                            ContentSeperatorAndSetter(sw, dt.Rows[i]["content"].ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Write the full header to file
        /// </summary>
        /// <param name="sw"></param>
        /// <param name="dt"></param>
        /// <param name="index"></param>
        private static void SetHeader(StreamWriter sw, DataTable dt,int index)
        {
            var titleConvert = dt.Rows[index]["title"].ToString().Replace(":", " - ");
            var reference = "";
            var isEmptyReference = true;

            sw.WriteLine("---");

            if (blog)
            {
                var teaser = dt.Rows[index]["teaser"].ToString().Replace(":", " - ").Replace("\n", " ").Replace("<",string.Empty).Replace(">",string.Empty);
                var date = Convert.ToDateTime(dt.Rows[index]["date"]).ToString("yyyy-MM-dd");
                var language = dt.Rows[index]["SourceCultureName"].ToString().Substring(0, 2);

                sw.WriteLine("layout: blog");
                sw.WriteLine($"title: {titleConvert}");
                sw.WriteLine($"excerpt: {teaser}");
                sw.WriteLine($"author: {dt.Rows[index]["name"]}");
                sw.WriteLine($"date: {date}");

                var imageUrl = "";

                //gets the image url from the guid of the image
                if (dt.Rows[index]["image"].ToString() != string.Empty)
                {
                    var guid = dt.Rows[index]["image"].ToString().Substring(13);

                    if (idWithPathsData.ContainsKey(guid))
                        imageUrl = idWithPathsData[guid];
                }

                sw.WriteLine($"bannerimage: {imageUrl}");

                var imagesource = dt.Rows[index]["imagesource"].ToString().Replace(":", " - ");
                MatchCollection replacedString = null;

                //Replace the - in the html tag
                if (imagesource != string.Empty)
                {
                    var originalString = Regex.Matches(imagesource, @"(<a)(.|\W|\s|\x22)+?(<\/a>)");

                    replacedString = originalString;

                    for (int i = 0; i < originalString.Count; i++)
                    {
                        var help = Regex.Replace(replacedString[i].ToString(), " - ", ":");
                        imagesource = imagesource.Replace(originalString[i].ToString(), help);
                    }
                }

                sw.WriteLine($"bannerimagesource: {imagesource}");
                sw.WriteLine($"lang: {language}");

                //Adds the all tags to header
                var tags = dt.Rows[index]["tags"].ToString().Split(',');

                var tagString = "[";

                for (int i = 0; i < tags.Length; i++)
                {
                    if (tags.Length - 1 != i)
                        tagString += $"{tags[i]},";
                    else
                        tagString += tags[i];
                }

                sw.WriteLine($"tags: {tagString}]");

                //Check if blog has references
                if (german)
                {
                    if ((reference = idWithPathsBlogs[dt.Rows[index]["id"].ToString()][1]) == string.Empty)
                        isEmptyReference = true;
                    else
                        isEmptyReference = false;
                }
                else
                {
                    if (dt.Rows[index]["devblog"].ToString() == "False")
                    {
                        if ((reference = idWithPathsBlogs[dt.Rows[index]["id"].ToString()][0]) == string.Empty)
                            isEmptyReference = true;
                        else
                            isEmptyReference = false;
                    }
                }

                if (!isEmptyReference)
                    sw.WriteLine($"ref: {reference}");
                else if(isEmptyReference)
                    sw.WriteLine($"ref: ");
            }
            else
            {
                sw.WriteLine("layout: page");
                sw.WriteLine($"title: {titleConvert}");

			    if (currentPathBlog == "/home/")
				    currentPathBlog = "/";

                //Checks if page has references
                if (german)
                {
                    if ((reference = idWithPathsGermanEnglish[dt.Rows[index]["id"].ToString()][1]) == string.Empty)
                        isEmptyReference = true;
                    else
                        isEmptyReference = false;
                }
                else
                {
                    if ((reference = idWithPathsGermanEnglish[dt.Rows[index]["id"].ToString()][0]) == string.Empty)
                        isEmptyReference = true;
                    else
                        isEmptyReference = false;
                }

                if (!isEmptyReference)
                    sw.WriteLine($"ref: {reference}");
                else
                    sw.WriteLine($"ref: ");
            }

            //Prints the permalink
            if (german)
                sw.WriteLine($"permalink: /de{currentPathBlog}");
            else
                sw.WriteLine($"permalink: {currentPathBlog}");

            sw.WriteLine("---");
            sw.WriteLine();
        }

        /// <summary>
        /// Remove the namespace
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        private static XDocument RemoveNameSpace(XDocument document)
        {
            foreach (XElement e in document.Root.DescendantsAndSelf())
            {
                if(e.Name.Namespace != XNamespace.None)
                {
                    e.Name = XNamespace.None.GetName(e.Name.LocalName);
                }
                if (e.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None).Any())
                {
                    e.ReplaceAttributes(e.Attributes().Select(a => a.IsNamespaceDeclaration ? null : a.Name.Namespace != XNamespace.None ? new XAttribute(XNamespace.None.GetName(a.Name.LocalName), a.Value) : a));
                }
            }
            return document;
        }

        /// <summary>
        /// Checks the content and replace it with new
        /// </summary>
        /// <param name="sw"></param>
        /// <param name="content"></param>
        private static void ContentSeperatorAndSetter(StreamWriter sw, string content)
        {
            var document = XDocument.Parse(content);
            var hasFunction = false;
            var finalCheck = false;
            int count = 0;
            //var xnm = new XmlNamespaceManager(new NameTable());
            //xnm.AddNamespace("x", "http://www.w3.org/1999/xhtml");

            var withoutNamespace = RemoveNameSpace(document);

            //var nodes = document.XPathSelectElement("/x:html/x:body",xnm).Elements();

            var nodes = withoutNamespace.XPathSelectElement("/html/body").Elements();

            foreach (var xe in nodes)
            {
                var xeString = xe.ToString();

                //Checks for any media (file) hyperlink
                xeString = MakeHyperLink(xeString, @"~?/media\([A-Za-z0-9]{8}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{12}\)", idWithPathsData);

                //Checks all links to other pages
                if (german)
                    xeString = MakeHyperLink(xeString, @"~?/page\([A-Za-z0-9]{8}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{12}\)", idWithPathsPagesGerman);
                else
                    xeString = MakeHyperLink(xeString, @"~?/page\([A-Za-z0-9]{8}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{12}\)", idWithPathsPagesEnglish);

                //Checks if blog has any code in the content
                if (blog)
                    xeString = MakeHighLight(xeString);

                hasFunction = PrintAllFunction(xeString);

                if (hasFunction)
                {
                    count++;
                    if(count == 1)
                        finalCheck = true;
                }

                sw.Write(xeString);
            }

            if(hasFunction)
                Console.WriteLine($"{currentPathBlog} beinhaltet funktion");
        }

        /// <summary>
        /// Just for checking if page have any function tags
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private static bool PrintAllFunction(string content)
        {
            var result1 = Regex.IsMatch(content, @"(<function\s{1}name=\x22)(.|\W|\s|\x22)+?(<\/function>)");

            if (result1)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Replace the code in a page to display it in jekyll
        /// </summary>
        /// <param name="xeString"></param>
        /// <returns></returns>
        private static string MakeHighLight(string xeString)
        {
            var fullCode = xeString;

            //Check if function have syntaxhighligher in tag
            var allResults = Regex.Matches(fullCode, @"(<function\s{1}name=\x22Composite\.Web\.Html\.SyntaxHighlighter\x22)(.|\W|\s)+?(<\/function>)");
            var code = "";

            foreach (var item in allResults)
            {
                var help = item.ToString();

                //Gets the param for source code and code type 
                var paramNodes = Regex.Matches(help, @"(<param\s{1}name=\x22SourceCode\x22).+?(\/>)");
                var paramCodeType = Regex.Matches(help, @"(<param\s{1}name=\x22CodeType\x22).+?(\/>)");

                if (paramNodes.Count != 0)
                {
                    foreach (var singleParam in paramNodes)
                    {
                        //Gets the value of the params
                        var valueNodes = Regex.Matches(singleParam.ToString(), @"(value=\W?).+?(\x22)");
                        var valueCodeType = Regex.Matches(paramCodeType[0].ToString(), @"(value=\W?).+?(\x22)");

                        foreach (var singleValue in valueNodes)
                        {
                            //Checks if source code and code type is in this format
                            code = Regex.Matches(singleValue.ToString(), @"(\x22).+?(\x22)")[0].Value;
                            var codeType = Regex.Matches(valueCodeType[0].ToString(), @"(\x22).+?(\x22)")[0].Value;

                            //remove the " 
                            if (code.StartsWith("\x22") && code.EndsWith("\x22"))
                            {
                                code = code.Substring(1, code.Length - 1);
                                code = code.Remove(code.Length - 1);
                            }

                            codeType = codeType.Substring(1, codeType.Length - 1);
                            codeType = codeType.Remove(codeType.Length - 1);

                            //Replacing chars
                            code = code.Replace("&#xA;", "\n").Replace("&#xD;", "\r").Replace("&quot;", "\x22").Replace("&#x9;", "    ").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

                            var highlight = "{% highlight "+codeType+" %}";

                            var highlightEnd = "{% endhighlight %}";

                            var newCode = $"{highlight}{code}{highlightEnd}";

                            xeString = fullCode.Replace(help, newCode);
                        }
                    }
                }
            }
            return xeString;
        }

        /// <summary>
        /// Replaces the old hyperlinks to another page or image for jekyll
        /// </summary>
        /// <param name="content"></param>
        /// <param name="pattern"></param>
        /// <param name="ids"></param>
        /// <returns></returns>
        private static string MakeHyperLink(string content,string pattern, Dictionary<string,string> ids)
        {
            var guid = Regex.Matches(content, pattern);

            if (guid.Count == 0)
                return content;
            else
            {
                foreach (var id in guid)
                {
                    var idString = id.ToString();

                    //Remove the ~
                    if (idString.StartsWith("~"))
                        idString = idString.Substring(1);

                    var singleGuid = idString.Substring(idString.IndexOf('(') + 1, 36);

                    if (ids.ContainsKey(singleGuid))
                    {
                        content = content.Replace("~", string.Empty);
                        content = content.Replace(idString, "{{site.baseurl}}" + ids[singleGuid]);
                    }
                    else
                        Console.WriteLine($"{idString} nicht verfügbar");
                }
                return content;
            }
        }

        /// <summary>
        /// Checks if directory exists
        /// </summary>
        /// <param name="path"></param>
        private static void CheckDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Checks if file exists
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool CheckFile(string path)
        {
            if (File.Exists(path)) return true;
            else return false;
        }

        /// <summary>
        /// Split the title
        /// </summary>
        /// <param name="urlTitle"></param>
        /// <returns></returns>
        private static string[] GetUrlTitle(string urlTitle)
        {
            return Regex.Split(urlTitle,"/");
        }
    }
}