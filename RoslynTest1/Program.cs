﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


/*
#######################################################################################################################################

RoslynTest1

Combining roslyn assemblies

NOTE: This project is based on the code posted by "Nick Polyak" at stackoverflow "https://stackoverflow.com/questions/37213781/compiling-classes-separately-using-roslyn-and-combining-them-together-in-an-asse/37239671" and also available at the roslyn site "https://github.com/dotnet/roslyn/issues/11297".

I did not make this code, I only did a visual stuio 2017 project to contain the original code submmited by "Nick Polyak" at stackoverflow in order to test it, and to share with him in order to ask for help because I can not make it work.

Thanks to Nick, now the code is working perfectly!!
I leave it here in case it could be useful for someone else.

#######################################################################################################################################
*/



namespace RoslynTest1
{
    public static class Program
    {
        // NOTE: I could not include the namespace "Microsoft.CodeAnalysis.CSharp.Test.Utilities", so I did a copy of some definitions required to thest this code.
        // See http://source.roslyn.io/#Roslyn.Compilers.CSharp.Test.Utilities/TestOptions.cs,0e91807b8f0500de
        public static class TestOptions
        {
            public static readonly CSharpCompilationOptions ReleaseExe = new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release);
            public static readonly CSharpCompilationOptions ReleaseModule = new CSharpCompilationOptions(OutputKind.NetModule, optimizationLevel: OptimizationLevel.Release);
        }
        /*
        public static class TestOptions
        {
            public static readonly CSharpCompilationOptions ReleaseExe =
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true);
            public static readonly CSharpCompilationOptions ReleaseModule =
                new CSharpCompilationOptions(OutputKind.NetModule, allowUnsafe: true);
        }
        */


        public static void Main()
        {
            try
            {
                var s1 = @"public class A {internal object o1 = new { hello = 1, world = 2 }; public static string M1() {    return ""Hello, "";}}";
                var s2 = @"public class B : A{internal object o2 = new { hello = 1, world = 2 };public static string M2(){    return ""world!"";}}";
                var s3 = @"public class Program{public static void Main(){    System.Console.Write(A.M1());    System.Console.WriteLine(B.M2());}}";

                var comp1 = CreateCompilationWithMscorlib("a1", s1, compilerOptions: TestOptions.ReleaseModule);
                byte[] comp1Result = comp1.EmitToArray();
                var ref1 = comp1Result.EmitToImageReference(OutputKind.NetModule, comp1.SourceModule.Name);

                var comp2 = CreateCompilationWithMscorlib("a2", s2, compilerOptions: TestOptions.ReleaseModule, references: new[] { ref1 });
                byte[] comp2Result = comp2.EmitToArray();
                var ref2 = comp2Result.EmitToImageReference(OutputKind.NetModule, comp2.SourceModule.Name);

                var comp3 = CreateCompilationWithMscorlib("a3", s3, compilerOptions: TestOptions.ReleaseExe.WithModuleName("C"), references: new[] { ref1, ref2 });

                byte[] result = comp3.EmitToArray();

                Assembly assembly = Assembly.Load(result.ToArray());

                assembly.LoadModule("a1.netmodule", comp1Result.ToArray());
                assembly.LoadModule("a2.netmodule", comp2Result.ToArray());


                Module module = assembly.GetModule("C");

                Type prog = module.GetType("Program");

                object instance = Activator.CreateInstance(prog);

                MethodInfo method = prog.GetMethod("Main");

                method.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static ImmutableArray<byte> ToImmutable(this MemoryStream stream)
        {
            return ImmutableArray.Create<byte>(stream.ToArray());
        }

        internal static byte[] EmitToArray
        (
            this Compilation compilation,
            EmitOptions options = null,
            Stream pdbStream = null
        )
        {
            var stream = new MemoryStream();

            if (pdbStream == null && compilation.Options.OptimizationLevel == OptimizationLevel.Debug)
            {
                pdbStream = new MemoryStream();
            }

            var emitResult = compilation.Emit(
                peStream: stream,
                pdbStream: pdbStream,
                xmlDocumentationStream: null,
                win32Resources: null,
                manifestResources: null,
                options: options);

            return stream.ToArray();
        }

        public static MetadataReference EmitToImageReference(
            this byte[] image,
            OutputKind outputKind,
            string displayName
        )
        {
            if (outputKind == OutputKind.NetModule)
            {
                return ModuleMetadata.CreateFromImage(image).GetReference(display: displayName);
            }
            else
            {
                return AssemblyMetadata.CreateFromImage(image).GetReference(display: displayName);
            }
        }

        private static CSharpCompilation CreateCompilationWithMscorlib(string assemblyName, string code, CSharpCompilationOptions compilerOptions = null, IEnumerable<MetadataReference> references = null)
        {
            SourceText sourceText = SourceText.From(code, Encoding.UTF8);
            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, null, "");

            MetadataReference mscoreLibReference = AssemblyMetadata.CreateFromFile(typeof(string).Assembly.Location).GetReference();

            IEnumerable<MetadataReference> allReferences = new MetadataReference[] { mscoreLibReference };

            if (references != null)
            {
                allReferences = allReferences.Concat(references);
            }

            CSharpCompilation compilation = CSharpCompilation.Create
            (
                assemblyName,
                new[] { syntaxTree },
                options: compilerOptions,
                references: allReferences
            );

            return compilation;
        }

    }
}
