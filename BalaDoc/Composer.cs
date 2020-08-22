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
    class Composer
    {
        private const int version = 0;

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

        public void ProcessDoc(string filename)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseYamlFrontMatter()
                .UseSyntaxHighlighting()
                .Build();

            var content = File.ReadAllText(filename);
            PostFront front = GetFront(content);
            front.Hash = GetHash(content); // this should be done before the function, later do it

            if (front.Date == null)
                front.Date = File.GetCreationTime(filename);
            front.LastEdit = File.GetLastWriteTime(filename);

            var doc = Markdown.Parse(content, pipeline);

            foreach(var block in doc)
            {
                if(block is Markdig.Syntax.HeadingBlock b && b.Level == 1)
                {
                    front.Title = (b.Inline.FirstChild as Markdig.Syntax.Inlines.LiteralInline).Content.ToString();

                    
                }
            }

            var result = Markdown.ToHtml(content, pipeline);

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(filename), "gen.html"), result);
        }
    }
}
