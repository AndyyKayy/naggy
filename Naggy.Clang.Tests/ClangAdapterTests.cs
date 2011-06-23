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
            var diags = adapter.GetDiagnostics();

            Assert.AreEqual(0, diags.Count);
        }

        [TestMethod]
        [Ignore]
        public void GetDiagnostics_MisspelledMemberName_DiagnosticIncludesSuggestedMember()
        {
            File.WriteAllText(sourceFilePath, @"struct A { int Foo; }; int main() { struct A a; a.Fo = 2; }");
            var adapter = new ClangAdapter(sourceFilePath);
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
            var preprocessor = adapter.GetPreprocessor();
            {
                Assert.AreEqual("2*y", preprocessor.ExpandMacro("foo"));
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDefDirectiveWithoutDefinition_SkippedBlockIncludesIfdefBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#ifdef x
#define foo x*y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().Single();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(2, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDirectiveWithoutDefinition_SkippedBlockIncludesIfBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 2 - 2 
#define foo x*y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().Single();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(2, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDirectiveWithElseDirectiveEvaluationFalse_SkippedBlockIncludesIfBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 2 - 2 
#define foo x*y
#else
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().Single();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(2, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDirectiveWithElseDirectiveEvaluationTrue_SkippedBlockIncludesElseBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 2 
#define foo x*y
#else
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().Single();
                Assert.AreEqual(4, skippedBlock.Item1);
                Assert.AreEqual(4, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDirectiveWithElifAndElseDirectiveEvaluationsTrue_SkippedBlockIncludesElseBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 0 
#define foo x*y
#elif 2
#define foo x+y
#else
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();

                var skippedBlock = skippedBlocks.First();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(2, skippedBlock.Item2);

                skippedBlock = skippedBlocks.ElementAt(1);
                Assert.AreEqual(6, skippedBlock.Item1);
                Assert.AreEqual(6, skippedBlock.Item2);

                
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfSingleElifDirectiveWithIfDirectiveFalse_SkippedBlockIncludesIfBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#define X 0
#define Y 1
#if X 
#define foo x*y
#elif Y
#define foo x/y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().Single();
                Assert.AreEqual(4, skippedBlock.Item1);
                Assert.AreEqual(4, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfMultipleElifDirectiveWithIfDirectiveFalseAndFirstElifDirectiveTrue_SkippedBlockIncludesFirstElifBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#define X 0
#define Y 1
#define Z 2
#if X 
#define foo x*y
#elif Y
#define foo x/y
#elif Z
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().First();
                Assert.AreEqual(5, skippedBlock.Item1);
                Assert.AreEqual(5, skippedBlock.Item2);

                skippedBlock = preprocessor.GetSkippedBlockLineNumbers().ElementAt(1);
                Assert.AreEqual(9, skippedBlock.Item1);
                Assert.AreEqual(9, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfMultipleElifDirectiveWithIfDirectiveFalseAndSecondElifDirectiveTrue_SkippedBlockIncludesSecondElifBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#define X 0
#define Y 0
#define Z 2
#if X 
#define foo x*y
#elif Y
#define foo x/y
#elif Z
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlock = preprocessor.GetSkippedBlockLineNumbers().First();
                Assert.AreEqual(5, skippedBlock.Item1);
                Assert.AreEqual(5, skippedBlock.Item2);

                skippedBlock = preprocessor.GetSkippedBlockLineNumbers().ElementAt(1);
                Assert.AreEqual(7, skippedBlock.Item1);
                Assert.AreEqual(7, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfSingleElifDirectiveWithElifDirectiveFalse_SkippedBlockIncludesElifBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#define X 1
#define Y 0
#define Z 0
#if X 
#define foo x*y
#elif Z + 0
#define foo x+y
#elif Y
#define foo x/y
#elif Z
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();
                var skippedBlock = skippedBlocks.First();
                Assert.AreEqual(7, skippedBlock.Item1);
                Assert.AreEqual(7, skippedBlock.Item2);

                skippedBlock = skippedBlocks.ElementAt(1);
                Assert.AreEqual(9, skippedBlock.Item1);
                Assert.AreEqual(9, skippedBlock.Item2);

                skippedBlock = skippedBlocks.ElementAt(2);
                Assert.AreEqual(11, skippedBlock.Item1);
                Assert.AreEqual(11, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfBlockWithMultiplePreprocessorDefinitionsIsTrue_SkippedBlockIsEmpty()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 1
#define foo x+y
#define fat boo
#define fal laf
#endif
#if 1
#define foo x+y
#define fat boo
#define fal laf
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();
                Assert.IsFalse(skippedBlocks.Any());
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfBlockWithMultiplePreprocessorDefinitions_SkippedBlockIncludesAllLines()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 0
#define foo x+y
#define fat boo
#define fal laf
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();
                var skippedBlock = skippedBlocks.First();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(4, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_TwoIfBlocksBothFalse_SkippedBlockIncludesBothBlocks()
        {
            File.WriteAllText(sourceFilePath, 
@"#if 0
#define foo x+y
#endif
#if 0
#define foo x-y
#endif
");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();
                var skippedBlock = skippedBlocks.First();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(2, skippedBlock.Item2);

                skippedBlock = skippedBlocks.ElementAt(1);
                Assert.AreEqual(5, skippedBlock.Item1);
                Assert.AreEqual(5, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDefinedBlockWithUndefinedMacro_SkippedBlockIncludesIfDefinedBlock()
        {
            File.WriteAllText(sourceFilePath, 
@"#if defined(OOLALALA)
#  include <avr/io.h>
#endif");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();
                var skippedBlock = skippedBlocks.First();
                Assert.AreEqual(2, skippedBlock.Item1);
                Assert.AreEqual(2, skippedBlock.Item2);
            }
        }

        [TestMethod]
        public void GetSkippedBlocks_IfDefinedBlockWithDefinedMacro_SkippedBlockIncludesIfDefinedBlock()
        {
            File.WriteAllText(sourceFilePath,
@"#if defined(__GNUC__)
#  include <avr/io.h>
#elif defined(__ICCAVR__)
#  include <ioavr.h>
#  include <intrinsics.h>
#else
#  error Unsupported compiler.
#endif");
            var adapter = new ClangAdapter(sourceFilePath);
            var preprocessor = adapter.GetPreprocessor();
            {
                var skippedBlocks = preprocessor.GetSkippedBlockLineNumbers();
                var skippedBlock = skippedBlocks.First();
                Assert.AreEqual(4, skippedBlock.Item1);
                Assert.AreEqual(5, skippedBlock.Item2);

                skippedBlock = skippedBlocks.ElementAt(1);

                Assert.AreEqual(7, skippedBlock.Item1);
                Assert.AreEqual(7, skippedBlock.Item2);
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
