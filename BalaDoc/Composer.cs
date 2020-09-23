using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.SyntaxHighlighting;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using System.Security.Cryptography;
using Markdig.Syntax;
using System.Linq;
using BalaDoc.Exception;
using System.Text.Json;
using Masuit.Tools.Strings;

namespace BalaDoc
{
    record DocumentInfo
    {
        public string Path { get; set; }
        public string Content { get; set; }
        public IList<string>? Category { get; set; }
        public DateTime? Date { get; set; }
        public string Title { get; set; }
        public DateTime? LastEdit { get; set; }
        public string Hash { get; set; }

        public void Update(DocumentInfo info)
        {
            var props = info.GetType().GetProperties();
            foreach (var prop in props)
            {
                prop.SetValue(this, prop.GetValue(info) ?? prop.GetValue(this));
            }
        }
    }

    class DocumentInfoComparer : IEqualityComparer<DocumentInfo>
    {
        // DocumentInfos are equal if their names and DocumentInfo numbers are equal.
        public bool Equals(DocumentInfo x, DocumentInfo y)
        {

            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            //Check whether the DocumentInfos' properties are equal.
            if (x.Path != y.Path)
                return false;   // Absolutely different
            var type = x.GetType();
            var props = type.GetProperties();
            foreach (var p in props)
            {
                var vx = p.GetValue(x); var vy = p.GetValue(y);
                if (vx is null || vy is null)   // null will be regard as same thing
                    continue;
                else if (vx != vy)
                    return false;
            }

            return true;
        }

        // If Equals() returns true for a pair of objects
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(DocumentInfo DocumentInfo)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(DocumentInfo, null)) return 0;

            //Get hash code for the Name field if it is not null.
            return DocumentInfo.Path == null ? 0 : DocumentInfo.Path.GetHashCode();
        }
    }

    public enum VerTestResult
    {
        Old, Current, Mirainokoto, Uninitialized
    }
    class Composer
    {
        private readonly int version = 0;
        private readonly string WorkDir;
        private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                            .UseAdvancedExtensions()
                            .UseYamlFrontMatter()
                            .UseSyntaxHighlighting()
                            .Build();
        private static readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .Build();

        private HashSet<string> Categories = new();
        private List<DocumentInfo> Documents = new();
        private Dictionary<string, int> Counts = new();

        private string TemplatePage;
        private string TemplateIndex;

        #region Directories and Files
        private static readonly string _NameDefaultTemplate = "Default";

        private static readonly string _DirSourceDoc = "doc";
        private static readonly string _DirTemplate = "template";
        private static readonly string _DirTemplateDefault = $"{_DirTemplate}/{_NameDefaultTemplate}";
        private static readonly string _DirMeta = ".inf";
        private static readonly string _DirMetaFileTracking = $"{_DirMeta}/track";
        private static readonly string _DirCategories = "category";
        private static readonly string _DirDestPages = "pages";

        private static readonly string _TemplateMetaUsing = $"{_DirMeta}/.";

        private static readonly string _FileMetaSerialNum = $"{_DirMeta}/sn";
        private static readonly string _FileCategoriesRecord = $"{_DirCategories}/categories.json";

        #endregion


        public Composer(string workDir)
        {
            WorkDir = workDir;
        }
        public static VerTestResult TestVersion(Composer c)
        {
            var version = c.GetCurrentVersion();
            return version switch
            {
                int when version > c.version => VerTestResult.Mirainokoto,
                int when version == c.version => VerTestResult.Current,
                int when version < c.version => VerTestResult.Old,
                _ => VerTestResult.Uninitialized
            };
        }
        public static void CurveVersion(Composer c)
        {
            File.WriteAllBytes(Path.Combine(c.WorkDir, _FileMetaSerialNum), BitConverter.GetBytes(c.version));
        }
        public static void CreateDirs(string baseDir, string[] dirs)
        {
            foreach (var d in dirs)
            {
                Directory.CreateDirectory(Path.Combine(baseDir, d));
            }
        }
        public static DocumentInfo GetFront(string content)
        {
            DocumentInfo front;
            using (var input = new StringReader(content))
            {
                var parser = new Parser(input);
                parser.Consume<StreamStart>();
                parser.Consume<DocumentStart>();
                front = yamlDeserializer.Deserialize<DocumentInfo>(parser);
                parser.Consume<DocumentEnd>();
            }

            return front;
        }
        public static byte[] GetBytes(string source)
        {
            return Encoding.UTF8.GetBytes(source);
        }
        public static string GetHash(string source)
        {
            var sb = new StringBuilder();
            using var sha = SHA256.Create();
            using var ms = new MemoryStream();
            var hashbs = sha.ComputeHash(GetBytes(source));
            foreach (byte b in hashbs)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public int? GetCurrentVersion()
        {
            byte[] bs;
            try
            {
                bs = File.ReadAllBytes(Path.Combine(WorkDir, _FileMetaSerialNum));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            return BitConverter.ToInt32(bs);
        }

        string CurrentTemplate { get => Directory.EnumerateFiles(_DirMeta).Select(path => Path.GetFileName(path)).Where(name => name[0] == '.').Select(name => name[1..^0]).FirstOrDefault(); }
        public string GetTemplate(string templateName = "") => templateName switch
        {
            "" => CurrentTemplate ?? _NameDefaultTemplate,
            string s => s
        };
        public void LoadTemplate(string templateName)
        {
            TemplatePage = File.ReadAllText(Path.Combine(WorkDir, _DirTemplate, templateName, "Tpage.html"));
            TemplateIndex = File.ReadAllText(Path.Combine(WorkDir, _DirTemplate, templateName, "Tindex.html"));

            File.WriteAllText(_TemplateMetaUsing + templateName, "");
        }

        public void DocProcess(DocumentInfo document)
        {
            document.Update(GetFront(document.Content));

            var doc = Markdown.Parse(document.Content, pipeline);

            // Use the first leaf block (containing plain text) content as the title
            // 80 character at maximum
            foreach (var block in doc)
            {
                if ((block is LeafBlock b) && (b.Inline is not null))
                {
                    document.Title = (b.Inline.FirstChild
                        as Markdig.Syntax.Inlines.LiteralInline)?.Content.ToString();
                    if (document.Title.Length > 80)
                        document.Title = document.Title.Substring(0, 80);
                    break;
                }
            }

            var content = Markdown.ToHtml(document.Content, pipeline);
            var dirPath = Path.Combine(WorkDir, _DirDestPages, document.Date?.ToString("yyyy-MM"));

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            var tPage = new Template(TemplatePage);
            tPage.Set("title", document.Title);
            tPage.Set("content", content);

            File.WriteAllText(Path.Combine(dirPath, $"{document.Title}.html"), tPage.Render());

            Documents.Add(document with { Content = "", Hash = "", Path = Path.GetFileName(document.Path) });
            foreach (var c in document.Category)
            {
                Categories.Add(c);
                Counts[c] = Counts.TryGetValue(c, out int n) ? n + 1 : 1;
            }
        }
        public bool DocCheckModified(string filename, string hash)
        {
            var rawfilename = Path.GetFileName(filename);
            var i = rawfilename.LastIndexOf('.');
            rawfilename = rawfilename[0..(i == -1 ? ^0 : i)];

            try
            {
                if (File.ReadAllText(Path.Combine(WorkDir, _DirMetaFileTracking, rawfilename)) == hash)
                {
                    return false;
                }
            }
            catch (System.Exception)
            {
            }
            return true;
        }
        public void DocRecord(string filename, string hash)
        {
            var rawfilename = Path.GetFileName(filename);
            var i = rawfilename.LastIndexOf('.');
            rawfilename = rawfilename[0..(i == -1 ? ^0 : i)];

            File.WriteAllText(Path.Combine(WorkDir, _DirMetaFileTracking, rawfilename), hash);
        }
        public void DataSave()
        {
            string? cateJson = null;
            List<DocumentInfo> cates;
            try
            {
                cateJson = File.ReadAllText(Path.Combine(WorkDir, _FileCategoriesRecord));
                cates = JsonSerializer.Deserialize<List<DocumentInfo>>(cateJson);

            }
            catch (FileNotFoundException)
            {
                cates = new();
            }


            cates = cates.Intersect(Documents, new DocumentInfoComparer()).Concat(Documents.Except(cates, new DocumentInfoComparer())).ToList();

            File.WriteAllText(Path.Combine(WorkDir, _FileCategoriesRecord), JsonSerializer.Serialize(cates, new() { IgnoreNullValues = true }));
        }

        public void InitDir()
        {
            if (Directory.EnumerateFileSystemEntries(WorkDir).Count() > 0)
            {
                throw new InitializeNonemptyDirectoryException();
            }
            CreateDirs(WorkDir, new string[] { _DirMeta, _DirMetaFileTracking, _DirSourceDoc, _DirTemplate, _DirTemplateDefault, _DirCategories, _DirDestPages });

            CurveVersion(this);
        }

        public void IndexGenerate()
        {
            var tIndex = new Template(TemplateIndex);
            tIndex.Set("PageName", "");

            File.WriteAllText(Path.Combine(WorkDir, _DirDestPages, "index.html"), tIndex.Render());
        }
        public void Generate(string templateName = "", bool completeGenerate = false)
        {
            if (TestVersion(this) != VerTestResult.Current)
                throw new ComposerVersionMismatchException();
            
            LoadTemplate(templateName = GetTemplate(templateName));
            if(completeGenerate || CurrentTemplate != templateName)
            {
                IndexGenerate();
            }
            // collect all documents
            var docs = Directory.EnumerateFiles(Path.Combine(WorkDir, _DirSourceDoc));
            foreach (var doc in docs)
            { 
                var content = File.ReadAllText(doc);
                var doc_hash = GetHash(content);

                if (!completeGenerate && !DocCheckModified(doc, doc_hash))
                {
                    Documents.Add(new() { Path = Path.GetFileName(doc) });
                    continue;
                }

                DocumentInfo thisDoc = new()
                {
                    Path = doc,
                    Content = content,
                    Hash = doc_hash,
                    Date = File.GetCreationTime(doc),
                    LastEdit = File.GetLastWriteTime(doc)
                };
                DocProcess(thisDoc);
                DocRecord(doc, doc_hash);
            }

            DataSave();
        }

        public void AddPage(string pageName)
        {
            File.WriteAllText(Path.Combine(WorkDir, _DirSourceDoc, pageName + ".md"), "");
        }
    }
}
