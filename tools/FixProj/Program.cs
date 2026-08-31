using System;
using System.IO;

namespace FixProj
{
    class Program
    {
        static void Main(string[] args)
        {
            string file = @"d:\sourcecode\build_diag.txt";
            bool inCsc = false;
            int count = 0;
            foreach (var line in File.ReadLines(file))
            {
                if (line.Contains("Task \"Csc\""))
                {
                    inCsc = true;
                    Console.WriteLine(line);
                }
                else if (inCsc)
                {
                    if (line.Contains("Squirrel"))
                    {
                        Console.WriteLine(line);
                    }
                    if (line.Contains("Output Item(s)"))
                    {
                        inCsc = false;
                        if (++count > 2) break;
                    }
                }
            }
        }
    }
}
