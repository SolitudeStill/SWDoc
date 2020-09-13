using System;
using System.IO;

namespace BalaDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var c = new Composer(Directory.GetCurrentDirectory());
            c.Generate();

            Console.WriteLine("Done.");
        }
    }
}
