﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using NaggyClang;

namespace Naggy.Clang.Tests
{
    [TestClass]
    public class ClangAdapterTests
    {
        string sourceFilePath;

        [TestMethod]
        public void GetDiagnostics_EmptyCFile_NoDiagnosticsReturned()
        {
            File.WriteAllText(sourceFilePath, "");
            var adapter = new ClangAdapter(sourceFilePath);
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(0, diags.Count);
        }

        [TestMethod]
        public void GetDiagnostics_CFileInDiskWithOneWarning_OneDiagnosticsReturned()
        {
            var sourceText =  "int func(){}";
            File.WriteAllText(sourceFilePath, sourceText);
            var adapter = new ClangAdapter(sourceFilePath);

            adapter.Process(null);
            
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(1, diags.Count);
        }

        [TestMethod]
        public void GetDiagnostics_CFileInDiskHasNoWarningsTextHasOneWarning_OneDiagnosticsReturned()
        {
            var sourceInFile =  "int func(){ return 0; }";
            var sourceInEditor =  "int func(){ }";

            File.WriteAllText(sourceFilePath, sourceInFile);
            var adapter = new ClangAdapter(sourceFilePath);
            adapter.Process(sourceInEditor);
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(1, diags.Count);
        }

        [TestMethod]
        public void GetDiagnostics_CFileInDiskHasPreprocessorCodeAndNoWarningsTextHasOneWarning_OneDiagnosticsReturned()
        {
            var sourceInFile =
@"#if defined(__GNUC__)
    int x = 20;
#elif defined (__ICCAVR__)
    int x = 30;
    int y = 20;
#else
#error Unsupported compiler
#endif
";
            var sourceInEditor =  sourceInFile + 
@"int func(){ }

int main(){}
";

            File.WriteAllText(sourceFilePath, sourceInFile);
            var adapter = new ClangAdapter(sourceFilePath);
            adapter.Process(sourceInFile);
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(0, diags.Count);

            adapter.Process(sourceInEditor);
            diags = adapter.GetDiagnostics();

            Assert.AreEqual(1, diags.Count);
            Assert.AreEqual(9, diags.First().StartLine);
        }

        [TestMethod]
        public void GetDiagnostics_CFileInDiskHasOneWarningTextHasNoWarning_OneDiagnosticsReturned()
        {
            var sourceInEditor =  "int func(){ return 0; }";
            var sourceInFile =  "int func(){}";

            File.WriteAllText(sourceFilePath, sourceInFile);
            var adapter = new ClangAdapter(sourceFilePath);
            adapter.Process(sourceInEditor);
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(0, diags.Count);
        }

        [TestMethod]
        public void GetDiagnostics_WarningMadeAndCorrectedInEditor_ZeroDiagnosticsReturned()
        {
            var initialSourceInEditor =  "int func(){ }";
            var currentSourceInEditor =  "int func(){ return 0; }";
            var sourceInFile =  "";

            File.WriteAllText(sourceFilePath, sourceInFile);
            var adapter = new ClangAdapter(sourceFilePath);
            var diags = adapter.GetDiagnostics();
            Assert.AreEqual(0, diags.Count);

            adapter.Process(initialSourceInEditor);
            diags = adapter.GetDiagnostics();
            Assert.AreEqual(1, diags.Count);

            adapter.Process(currentSourceInEditor);
            diags = adapter.GetDiagnostics();
            Assert.AreEqual(0, diags.Count);
        }

        [TestMethod]
        public void GetDiagnostics_IntFunctionNotReturningAValue_DiagnosticDetailsAreCorrect()
        {
            File.WriteAllText(sourceFilePath, @"
/* Some source file */
int fun() {
}
");
            var adapter = new ClangAdapter(sourceFilePath);
            adapter.Process(null);
            var diag = adapter.GetDiagnostics().Single();
            Assert.AreEqual(sourceFilePath, diag.FilePath);
            Assert.AreEqual(4, diag.StartLine);
            Assert.AreEqual(1, diag.StartColumn);
        }

        [TestMethod]
        public void GetDiagnostics_SymbolInSourceCodeProvidedInPredefinedSymbolList_NoDiagnosticsReturned()
        {
            File.WriteAllText(sourceFilePath, @"int main() { return FOO; }");
            var adapter = new ClangAdapter(sourceFilePath, new List<string>(), new List<string>() { "FOO=2" });
            adapter.Process(null);
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(0, diags.Count);
        }

        [TestMethod]
        [Ignore]
        public void GetDiagnostics_MisspelledMemberName_DiagnosticIncludesSuggestedMember()
        {
            File.WriteAllText(sourceFilePath, @"struct A { int Foo; }; int main() { struct A a; a.Fo = 2; }");
            var adapter = new ClangAdapter(sourceFilePath);
            adapter.Process(null);
            var diags = adapter.GetDiagnostics();

            StringAssert.Contains(diags.First().Message, "did you mean 'Foo'?");
        }

        [TestMethod]
        public void ExpandMacro_MacroDefinitionIncludesAnotherMacro_ExpansionExpandsInnerMacro()
        {
            File.WriteAllText(sourceFilePath, @"
#define x 2
#define foo x*y
");
            var adapter = new ClangAdapter(sourceFilePath);
            adapter.Process(null);
            var preprocessor = adapter.GetPreprocessor();
            {
                Assert.AreEqual("2*y", preprocessor.ExpandMacro("foo"));
            }
        }

        [TestInitialize]
        public void Setup()
        {
            sourceFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".c");
        }

        [TestCleanup]
        public void Cleanup()
        {
            File.Delete(sourceFilePath);
        }
    }
}
