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
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;

namespace GetDataFromServerAndSaveInFile
{
    public class Program
    {
        private static bool german = true;
        private static bool blog = true;
        private static Dictionary<string, string> idWithPathsPages = new Dictionary<string, string>();
        private static Dictionary<string, string> idWithPathsData = new Dictionary<string, string>();
        private static Dictionary<string, string[]> idWithPathsBlogs = new Dictionary<string, string[]>();
        private static string[] filesToIgnore;

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

                var blogEntriesGerman = @"SELECT p.id,p.TitleUrl,p.title,p.image,p.Teaser,p.date,p.Author,p.Content,p.SourceCultureName,pp.name FROM Composite_Community_Blog_Entries_de_AT p inner join Composite_Community_Blog_Authors_Published pp on p.author = pp.id;";
                var blogEntriesEnglish = @"SELECT p.id,p.TitleUrl,p.title,p.image,p.Teaser,p.date,p.Author,p.Content,p.SourceCultureName,pp.name FROM Composite_Community_Blog_Entries_en_US p inner join Composite_Community_Blog_Authors_Published pp on p.author = pp.id; ";

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

                        FillDictionary(dataTableGerman, urlDataTableGerman);
                        FillDictionary(dataTableEnglish, urlDataTableEnglish);

                        CheckDirectory(ConfigurationManager.AppSettings["DeRootUrl"]);
                        CheckDirectory(ConfigurationManager.AppSettings["EnRootUrl"]);

                        AddFilesToIgnore();

                        DownloadData(urlDataTableMediaFile);

                        MakeBlog(dataTableBlogEntriesGerman, "DeRootUrl");

                        german = false;

                        MakeBlog(dataTableBlogEntriesEnglish, "EnRootUrl");

                        german = true;
                        blog = false;

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
			filesToIgnore = new string[] { "/de/blog/", "/blog/" };
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

        private static void MakeBlog(DataTable dt, string rootUrl)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DateTime date = Convert.ToDateTime(dt.Rows[i]["date"]);

                var path = "/blog/";
                var month = date.Month.ToString();
                var day = date.Day.ToString();

                if (date.Month.ToString().Length == 1)
                    month = $"0{date.Month}";
                if(date.Day.ToString().Length == 1)
                    day = $"0{date.Day}";

                path += $"{date.Year}/{month}/{day}";

                CheckDirectory($"{ConfigurationManager.AppSettings[rootUrl]}{path}");

                var fullpath = $"{path}/{dt.Rows[i]["titleUrl"]}";

                var id = dt.Rows[i]["id"].ToString();

                if (idWithPathsBlogs.ContainsKey(id))
                    idWithPathsBlogs[id][1] = fullpath;
                else if (german)
                    idWithPathsBlogs.Add(id, new string[] { $"/de{fullpath}", "" });
                else
                    idWithPathsBlogs.Add(id, new string[] { "", fullpath });

                if (CheckToIgnore(fullpath) <= -1)
                {
                    using (var sw = new StreamWriter($"{ConfigurationManager.AppSettings[rootUrl]}{fullpath}.md"))
                    {
                        SetHeader(sw, dt, i, fullpath);
                        ContentSeperatorAndSetter(sw, dt.Rows[i]["content"].ToString());
                    }
                }
            }
        }

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

                    var fullPath = $"{ConfigurationManager.AppSettings["RootUrl"]}{folder}";

                    CheckDirectory(fullPath);

                    fullPath = $"{fullPath}{dt.Rows[i]["fileName"]}";

                    if (!CheckFile(fullPath))
                    {
                        var data = client.DownloadData(dt.Rows[i]["id"].ToString());
                        File.WriteAllBytes(fullPath, data);
                    }
                }
            }
        }   

        private static void FillDictionary(DataTable dt, DataTable urlDt)
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
                else
                {
                    for (int j = 0; j < splitUrl.Length - 1; j++)
                    {
                        if (splitUrl[j] != string.Empty)
                            folderPath += splitUrl[j] + "/";
                    }
                    fileName = splitUrl[splitUrl.Length - 1];
                }
                helpPath = $"{folderPath}{fileName}/";

                if (!idWithPathsPages.ContainsKey(foundrow[0]["id"].ToString()))
                    idWithPathsPages.Add(foundrow[0]["id"].ToString(), helpPath);
            }
        }

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

                if (CheckToIgnore(helpPath) <= -1)
                {
                    if (dt.Rows[i]["placeholderid"].ToString() == "content")
                    {
                        CheckDirectory($"{ConfigurationManager.AppSettings[rootUrl]}{folderPath}");

                        using (var sw = new StreamWriter($"{ConfigurationManager.AppSettings[rootUrl]}{folderPath}{fileName}.md"))
                        {
                            SetHeader(sw, dt, i, helpPath);
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

        private static void SetHeader(StreamWriter sw, DataTable dt,int index, string fullPath)
        {
            var titleConvert = dt.Rows[index]["title"].ToString().Replace(":", " - ");

            sw.WriteLine("---");

            if (blog)
            {
                var teaser = dt.Rows[index]["teaser"].ToString().Replace(":", " - ").Replace("\n", " ");
                var date = Convert.ToDateTime(dt.Rows[index]["date"]).ToString("yyyy-MM-dd");
                var language = dt.Rows[index]["SourceCultureName"].ToString().Substring(0, 2);
                var reference = "";
                var isEmptyReference = true;

                sw.WriteLine("layout: blog");
                sw.WriteLine($"title: {titleConvert}");
                sw.WriteLine($"teaser: {teaser}");
                sw.WriteLine($"author: {dt.Rows[index]["name"]}");
                sw.WriteLine($"date: {date}");

                var imageUrl = "";

                if (dt.Rows[index]["image"].ToString() != string.Empty)
                {
                    var guid = dt.Rows[index]["image"].ToString().Substring(13);

                    if (idWithPathsData.ContainsKey(guid))
                        imageUrl = idWithPathsData[guid];
                }

                sw.WriteLine($"bannerimage: {imageUrl}");
                sw.WriteLine($"lang: {language}");

                if (german)
                {
                    if ((reference = idWithPathsBlogs[dt.Rows[index]["id"].ToString()][1]) == string.Empty)
                        isEmptyReference = true;
                    else
                        isEmptyReference = false;
                }
                else
                {
                    if ((reference = idWithPathsBlogs[dt.Rows[index]["id"].ToString()][0]) == string.Empty)
                        isEmptyReference = true;
                    else
                        isEmptyReference = false;
                }

                if(!isEmptyReference)
                    sw.WriteLine($"ref: {reference}");
            }
            else
            {
                sw.WriteLine("layout: page");
                sw.WriteLine($"title: {titleConvert}");
            }

			if (fullPath == "/home/")
				fullPath = "/";

            if (german)
                sw.WriteLine($"permalink: /de{fullPath}");
            else
                sw.WriteLine($"permalink: {fullPath}");

            sw.WriteLine("---");
            sw.WriteLine();
        }

        private static void ContentSeperatorAndSetter(StreamWriter sw, string content)
        {
            var document = XDocument.Parse(content);
            var xnm = new XmlNamespaceManager(new NameTable());
            xnm.AddNamespace("x", "http://www.w3.org/1999/xhtml");

            var nodes = document.XPathSelectElement("/x:html/x:body", xnm).Elements();

            foreach (var xe in nodes)
            {
                var xeString = xe.ToString();

                xeString = MakeHyperLink(xeString, @"~?/media\([A-Za-z0-9]{8}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{12}\)", idWithPathsData);

                xeString = MakeHyperLink(xeString, @"~?/page\([A-Za-z0-9]{8}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{4}\-[A-Za-z0-9]{12}\)", idWithPathsPages);

                if (blog)
                    xeString = MakeHighLight(xeString);

                sw.Write(xeString);
            }
        }

        private static string MakeHighLight(string xeString)
        {
            var fullCode = xeString;
            var allResults = Regex.Matches(fullCode, @"(<f:function\s{1}name=\x22Composite\.Web\.Html\.SyntaxHighlighter\x22)(.|\W|\s)+?(<\/f:function>)");
            var code = "";

            foreach (var item in allResults)
            {
                var help = item.ToString();

                var paramNodes = Regex.Matches(help, @"(<f:param\s{1}name=\x22SourceCode\x22).+?(\/>)");

                if (paramNodes.Count != 0)
                {
                    foreach (var singleParam in paramNodes)
                    {
                        var valueNodes = Regex.Matches(singleParam.ToString(), @"(value=\W?).+?(\x22)");

                        foreach (var singleValue in valueNodes)
                        {
                            code = Regex.Matches(singleValue.ToString(), @"(\x22).+?(\x22)")[0].Value;

                            if (code.StartsWith("\x22") && code.EndsWith("\x22"))
                            {
                                code = code.Substring(1, code.Length - 1);
                                code = code.Remove(code.Length - 1);
                            }

                            code = code.Replace("&#xA;", "\r\n");
                            code = code.Replace("&#x9;", "    ");

                            var highlight = "{% highlight javascript %}";

                            var highlightEnd = "{% endhighlight %}";

                            var newCode = $"{highlight}{code}{highlightEnd}";

                            xeString = fullCode.Replace(help, newCode);
                        }
                    }
                }
            }
            return xeString;
        }

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

        private static void CheckDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static bool CheckFile(string path)
        {
            if (File.Exists(path)) return true;
            else return false;
        }

        private static string[] GetUrlTitle(string urlTitle)
        {
            return Regex.Split(urlTitle,"/");
        }
    }
}