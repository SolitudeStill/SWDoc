using System;

namespace BalaDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var c = new Composer();
            c.ProcessDoc(Console.ReadLine());

            Console.WriteLine("Done.");
        }
    }
}
