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
        #region Directories
        private static readonly string _DirSourceDoc = "doc";
        private static readonly string _DirTemplate = "template";
        private static readonly string _DirTemplateDefault = $"{_DirTemplate}/Default";
        private static readonly string _DirMeta = ".inf";
        private static readonly string _DirMetaFileTracking = $"{_DirMeta}/track";
        private static readonly string _DirCategories = "category";
        private static readonly string _DirDestPages = "pages";

        private static readonly string _FileMetaSerialNum = $"{_DirMeta}/sn";
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
        [Obsolete]
        public void DocProcess(string filename)
        {
            var content = File.ReadAllText(filename);
            DocumentInfo front = GetFront(content);
            front.Hash = GetHash(content); // TODO: this should be done before the function, later do it

            if (front.Date == null)
                front.Date = File.GetCreationTime(filename);
            front.LastEdit = File.GetLastWriteTime(filename);

            var doc = Markdown.Parse(content, pipeline);

            // Use the first leaf block (containing plain text) content as the title
            // 80 character at maximum
            foreach (var block in doc)
            {
                if (block is LeafBlock b)
                {
                    front.Title = (b.Inline.FirstChild
                        as Markdig.Syntax.Inlines.LiteralInline)?.Content.ToString();
                    if (front.Title.Length > 80)
                        front.Title = front.Title.Substring(0, 80);
                    break;
                }
            }

            var result = Markdown.ToHtml(content, pipeline);

            // TODO: Add template filling
            File.WriteAllText(Path.Combine(WorkDir, _DirDestPages, front.Date?.ToString("yyyy-MM"), $"{front.Title}.html"), result);
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

            var result = Markdown.ToHtml(document.Content, pipeline);
            var dirPath = Path.Combine(WorkDir, _DirDestPages, document.Date?.ToString("yyyy-MM"));

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            // TODO: Add template filling
            File.WriteAllText(Path.Combine(dirPath, $"{document.Title}.html"), result);
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
        public void InitDir()
        {
            if (Directory.EnumerateFileSystemEntries(WorkDir).Count() > 0)
            {
                throw new InitializeNonemptyDirectoryException();
            }
            CreateDirs(WorkDir, new string[] { _DirMeta, _DirMetaFileTracking, _DirSourceDoc, _DirTemplate, _DirTemplateDefault, _DirCategories, _DirDestPages });

            CurveVersion(this);
        }

        public void Generate(bool completeGenerate = false)
        {
            if (TestVersion(this) != VerTestResult.Current)
                throw new ComposerVersionMismatchException();

            // collect all documents
            var docs = Directory.EnumerateFiles(Path.Combine(WorkDir, _DirSourceDoc));
            foreach (var doc in docs)
            {
                // TODO: IO operation will be meaninglessly excuted twice, need refactoring
                //  and read all text right now is also dirty here
                var content = File.ReadAllText(doc);
                var doc_hash = GetHash(content);

                if (!completeGenerate && !DocCheckModified(doc, doc_hash))
                {
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
        }
    }
}
