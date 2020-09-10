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

namespace BalaDoc
{
    class PostFront
    {
        public IList<string>? Category { get; set; }

        public DateTime? Date { get; set; }

        public string Title { get; set; }

        public DateTime? LastEdit { get; set; }

        public string Hash { get; set; }
    }
    public enum VerTestResult
    {
        Old, Current, Mirainokoto, Uninitialized
    }
    class Composer
    {
        private readonly int version = 0;
        private readonly string WorkDir;
        #region Directories
        private static readonly string _DirSourceDoc = "doc";
        private static readonly string _DirTemplate = "template";
        private static readonly string _DirTemplateDefault = $"{_DirTemplate}/Default";
        private static readonly string _DirMeta = "./inf";
        private static readonly string _DirMetaFileTracking = $"{_DirMeta}/track";
        private static readonly string _DirCategories = "category";
        private static readonly string _DirDestPages = "pages";

        private static readonly string _FileMetaSerialNum = $"{_DirMeta}/sn";
        #endregion

        public Composer(string WorkDir)
        {
            WorkDir = Path.GetDirectoryName(WorkDir);
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
            var dir = Path.GetDirectoryName(baseDir);

            foreach (var d in dirs)
            {
                Directory.CreateDirectory(Path.Combine(dir, d));
            }
        }
        public static PostFront GetFront(string content)
        {
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            PostFront front;
            using (var input = new StringReader(content))
            {
                var parser = new Parser(input);
                parser.Consume<StreamStart>();
                parser.Consume<DocumentStart>();
                front = yamlDeserializer.Deserialize<PostFront>(parser);
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
        public void ProcessDoc(string filename)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseYamlFrontMatter()
                .UseSyntaxHighlighting()
                .Build();

            var content = File.ReadAllText(filename);
            PostFront front = GetFront(content);
            front.Hash = GetHash(content); // TODO: this should be done before the function, later do it

            if (front.Date == null)
                front.Date = File.GetCreationTime(filename);
            front.LastEdit = File.GetLastWriteTime(filename);

            var doc = Markdown.Parse(content, pipeline);

            // Use the first leaf block (containing plain text) content as the title
            // 80 character at maximum
            foreach (var block in doc)
            {
                if(block is LeafBlock b)
                {
                    front.Title = (b.Inline.FirstChild as Markdig.Syntax.Inlines.LiteralInline)?.Content.ToString();
                    if (front.Title.Length > 80)
                        front.Title = front.Title.Substring(0, 80);
                    break;
                }
            }
            
            var result = Markdown.ToHtml(content, pipeline);

            // the directory will be based on "CurrentDirectory"
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(filename), "../page", "gen.html"), result);
        }

        public void InitDir(string route)
        {
            string[] dirs = { _DirMeta, _DirMetaFileTracking, _DirSourceDoc, _DirTemplate, _DirTemplateDefault, _DirCategories, _DirDestPages };
            CreateDirs(WorkDir,dirs);

            CurveVersion(this);
        }
    }
}
