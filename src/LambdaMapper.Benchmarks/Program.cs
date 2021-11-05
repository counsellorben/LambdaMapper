using System;
using System.Collections.Generic;
using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace LambdaMapper.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
