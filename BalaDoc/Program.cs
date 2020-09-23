using System;
using System.IO;

using CommandLine;

namespace BalaDoc
{
    [Verb("init", HelpText = "Initialize the directory.")]
    class InitOption
    {
    }
    [Verb("build", HelpText ="Generate page(s).")]
    class BuildOption
    {
        [Option('t', "template", Required = true, Default = "", HelpText = "Indicate the template to generate pages.")]
        public string Template { get; set; }
        [Option('a', "all", Required = false, HelpText = "Generate pages completely, whether modified or not.")]
        public bool CompleteGenerate { get; set; }
    }
    [Verb("add", HelpText = "Add new page.")]
    class PageOption
    {
        [Value(0, HelpText = "This name is source document name, not strictly to be the name of the page generated.")]
        public string FileName { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            var c = new Composer(Directory.GetCurrentDirectory());

            var exitCode = Parser.Default.ParseArguments<InitOption, BuildOption, PageOption>(args)
                .MapResult(
                    (PageOption o) => {
                        c.AddPage(o.FileName);
                        return 0;
                    },
                    (InitOption o) => {
                        c.InitDir();
                        return 0;
                    },
                    (BuildOption o) => {
                        c.Generate(o.Template, o.CompleteGenerate);
                        return 0;
                    },
                    err => -1
                );

            Console.WriteLine("Done.");
        }
    }
}
