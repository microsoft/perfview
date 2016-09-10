// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
using System;
// using System.Collections.Generic;

class Program
{
    public static int aStatic = 0;
    // Spin is a simple compute bound program that lasts for 5 seconds
    // It is a useful test program for CPU profilers.  
    static int Main(string[] args)
    {
        int numSec = 5;
        if (args.Length == 1)
            numSec = int.Parse(args[0]);

        Console.WriteLine("Spinning for {0} seconds", numSec);
        RecSpin(numSec);
        return 0;
    }

    // Spin for 'timeSec' seconds.   We do only 1 second in this
    // method, doing the rest in the helper.   
    static void RecSpin(int timeSec)
    {
        if (timeSec <= 0)
            return;
        --timeSec;
        SpinForASecond();
        RecSpinHelper(timeSec);
    }

    // RecSpinHelper is a clone of RecSpin.   It is repeated 
    // to simulate mutual recursion (more interesting example)
    static void RecSpinHelper(int timeSec)
    {
        if (timeSec <= 0)
            return;
        --timeSec;
        SpinForASecond();
        RecSpin(timeSec);
    }

    // SpingForASecond repeatedly calls DateTime.Now until for
    // 1 second.  It also does some work of its own in this
    // methods so we get some exclusive time to look at.  
    static void SpinForASecond()
    {
        DateTime start = DateTime.Now;
        for (; ; )
        {
            if ((DateTime.Now - start).TotalSeconds > 1)
                break;

            // Do some work in this routine as well.   
            for (int i = 0; i < 10; i++)
                aStatic += i;
        }
    }
}

