﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Quantum.CsharpGeneration.Testing

open System
open System.Collections.Immutable
open System.IO
open System.Globalization
open System.Web

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.Formatting
open Microsoft.VisualStudio.LanguageServer.Protocol

open Xunit

open Microsoft.Quantum.QsCompiler.CompilationBuilder
open Microsoft.Quantum.QsCompiler.CsharpGeneration
open Microsoft.Quantum.QsCompiler.CsharpGeneration.SimulationCode
open Microsoft.Quantum.QsCompiler.DataTypes
open Microsoft.Quantum.QsCompiler.ReservedKeywords
open Microsoft.Quantum.QsCompiler.SyntaxTree


module SimulationCode =
    open Microsoft.Quantum.QsCompiler

    let clearFormatting (str:string) =
        str
        |> Seq.filter (not << Char.IsWhiteSpace)
        |> String.Concat

    [<Fact>]
    let ``Pure roslyn``() =
        let code = "
        namespace N1
        {
        enum E { A, b, C }

        public class C1
        {
        public   object P1 {get;set;}
        }
        }
"

        let expected = "
namespace N1
{
    enum E { A, b, C }

    public class C1
    {
        public object P1 { get; set; }
    }
}
"
        let root = SyntaxFactory.ParseCompilationUnit(code)
        let ws = new AdhocWorkspace()
        let formattedRoot = Formatter.Format(root, ws)
        let actual = formattedRoot.ToFullString()
        Assert.Equal(expected |> clearFormatting, actual |> clearFormatting)

    [<Fact>]
    let ``doubles in different locales`` () =
        let cases =
            [
                1.1,       "1.1D"
                1000.001,  "1000.001D"
                -1000.001, "-1000.001D"
                float 1,   "1D"
                float 0,   "0D"
                float -1,  "-1D"
            ]
        let values   = cases |> List.map fst
        let expected = cases |> List.map snd
        let testCulture c =
            // The prefix is just to make the error tell which culture failed
            let addPrefix v =  c + ": " + v
            let savedCulture = CultureInfo.CurrentCulture
            CultureInfo.CurrentCulture <- CultureInfo(c)
            let actual = values |> List.map floatToString
            CultureInfo.CurrentCulture <- savedCulture
            List.zip (expected |> List.map addPrefix) (actual |> List.map addPrefix)
            |> List.iter Assert.Equal
        [ "ru-RU"; "en-UD"; "en-GB"; "fr-FR"; "da-DK"; "nl-NL"; "zh-CN"; "es-ES"; "ta-LK" ] |> List.iter testCulture

    let parse files =
        let mutable errors = [] : Diagnostic list
        let addError (diag : Diagnostic) =
            match diag.Severity with
            | DiagnosticSeverity.Error -> errors <- diag :: errors
            | _ -> ()
        let addSourceFile (mgr:CompilationUnitManager) fileName =
            let fileId = new Uri(Path.GetFullPath fileName)
            let file = CompilationUnitManager.InitializeFileManager(fileId, File.ReadAllText fileName)
            mgr.AddOrUpdateSourceFileAsync file |> ignore
            // TODO: catch compilation errors and fail
        let mgr   = new CompilationUnitManager(null, fun ps -> ps.Diagnostics |> Array.iter addError)
        files |> List.iter (addSourceFile mgr)
        try let mutable compilation = mgr.Build().BuiltCompilation
            if not errors.IsEmpty then
                errors
                |> List.map (fun e -> sprintf "%s at %s, line %d" e.Message e.Source (e.Range.Start.Line + 1))
                |> String.concat "\n"
                |> failwith
            let functorGenSuccessful = CodeGeneration.GenerateFunctorSpecializations(compilation, &compilation)
            // todo: we might want to raise an error here if the functor generation fails (which will be the case for incorrect code)
            compilation.Namespaces
        with | e -> sprintf "compilation threw exception: \n%s" e.Message |> failwith // should never happen (all exceptions are caught by the compiler)

    let syntaxTree = parse [ (Path.Combine("Circuits", "Intrinsic.qs")); (Path.Combine("Circuits", "CodegenTests.qs")) ]

    let globalContext = CodegenContext.Create syntaxTree

    let findCallable name =
        let key = NonNullable<string>.New name
        match globalContext.byName.TryGetValue key with
        | true, v -> v |> List.sort |> List.head
        | false, _ -> sprintf "no callable with name %s has been successfully compiled" name |> failwith

    let findUdt name =
        let key = globalContext.allUdts.Keys |> Seq.sort |> Seq.find (fun n -> n.Name.Value = name)
        match globalContext.allUdts.TryGetValue key with
        | true, v -> key.Namespace, v
        | false, _ -> sprintf "no type with name %s has been successfully compiled" name |> failwith

    ////
    // Create some operations for our tests...
    ////
    let emptyOperation                          = findCallable @"emptyOperation"
    let zeroQubitOperation                      = findCallable @"zeroQubitOperation"
    let oneQubitAbstractOperation               = findCallable @"oneQubitAbstractOperation"
    let oneQubitSelfAdjointAbstractOperation    = findCallable @"oneQubitSelfAdjointAbstractOperation"
    let randomAbstractOperation                 = findCallable @"randomAbstractOperation"
    let oneQubitSelfAdjointOperation            = findCallable @"oneQubitSelfAdjointOperation"
    let oneQubitOperation                       = findCallable @"oneQubitOperation"
    let twoQubitOperation                       = findCallable @"twoQubitOperation"
    let threeQubitOperation                     = findCallable @"threeQubitOperation"
    let differentArgsOperation                  = findCallable @"differentArgsOperation"
    let randomOperation                         = findCallable @"randomOperation"
    let ifOperation                             = findCallable @"ifOperation"
    let foreachOperation                        = findCallable @"foreachOperation"
    let repeatOperation                         = findCallable @"repeatOperation"
    let selfInvokingOperation                   = findCallable @"selfInvokingOperation"
    let letsOperations                          = findCallable @"letsOperations"
    let arraysOperations                        = findCallable @"arraysOperations"
    let sliceOperations                         = findCallable @"sliceOperations"
    let rangeOperations                         = findCallable @"rangeOperations"
    let helloWorld                              = findCallable @"helloWorld"
    let allocOperation                          = findCallable @"allocOperation"
    let failedOperation                         = findCallable @"failedOperation"
    let compareOps                              = findCallable @"compareOps"
    let partialApplicationTest                  = findCallable @"partialApplicationTest"
    let opParametersTest                        = findCallable @"opParametersTest"
    let measureWithScratch                      = findCallable @"measureWithScratch"
    let with1C                                  = findCallable @"With1C"
    let genC1                                   = findCallable @"genC1"
    let genC1a                                  = findCallable @"genC1a"
    let genC2                                   = findCallable @"genC2"
    let genAdj1                                 = findCallable @"genAdj1"
    let genCtrl3                                = findCallable @"genCtrl3"
    let genU1                                   = findCallable @"genU1"
    let genU2                                   = findCallable @"genU2"
    let genMapper                               = findCallable @"genMapper"
    let genIter                                 = findCallable @"genIter"
    let usesGenerics                            = findCallable @"usesGenerics"
    let callsGenericWithMultipleTypeParams      = findCallable @"callsGenericWithMultipleTypeParams"
    let duplicatedDefinitionsCaller             = findCallable @"duplicatedDefinitionsCaller"
    let nestedArgTuple1                         = findCallable @"nestedArgTuple1"
    let nestedArgTuple2                         = findCallable @"nestedArgTuple2"
    let nestedArgTupleGeneric                   = findCallable @"nestedArgTupleGeneric"
    let udtsTest                                = findCallable @"udtsTest"
    let compose                                 = findCallable @"compose"
    let composeImpl                             = findCallable @"composeImpl"
    let callTests                               = findCallable @"callTests"
    let udtTuple                                = findCallable @"udtTuple"
    let emptyFunction                           = findCallable @"emptyFunction"
    let intFunction                             = findCallable @"intFunction"
    let powFunction                             = findCallable @"powFunction"
    let bigPowFunction                          = findCallable @"bigPowFunction"
    let factorial                               = findCallable @"factorial"
    let genF1                                   = findCallable @"genF1"
    let genRecursion                            = findCallable @"genRecursion"
    let partialFunctionTest                     = findCallable @"partialFunctionTest"
    let returnTest1                             = findCallable @"returnTest1"
    let returnTest2                             = findCallable @"returnTest2"
    let returnTest3                             = findCallable @"returnTest3"
    let returnTest4                             = findCallable @"returnTest4"
    let returnTest5                             = findCallable @"returnTest5"
    let returnTest6                             = findCallable @"returnTest6"
    let returnTest7                             = findCallable @"returnTest7"
    let returnTest8                             = findCallable @"returnTest8"
    let returnTest9                             = findCallable @"returnTest9"
    let returnTest10                            = findCallable @"returnTest10"
    let bitOperations                           = findCallable @"bitOperations"
    let testLengthDependency                    = findCallable @"testLengthDependency"
    let UpdateUdtItems                          = findCallable @"UpdateUdtItems"
    let emptyInternalFunction                   = findCallable @"EmptyInternalFunction"
    let emptyInternalOperation                  = findCallable @"EmptyInternalOperation"
    let useInternalCallables                    = findCallable @"UseInternalCallables"

    let udt_args0                               = findUdt @"udt_args0"
    let udt_args1                               = findUdt @"udt_args1"
    let udt_A                                   = findUdt @"A"
    let udt_AA                                  = findUdt @"AA"
    let udt_U                                   = findUdt @"U"
    let udt_Q                                   = findUdt @"Q"
    let udt_QQ                                  = findUdt @"QQ"
    let udt_Qubits                              = findUdt @"Qubits"
    let udt_Real                                = findUdt @"udt_Real"
    let udt_Complex                             = findUdt @"udt_Complex"
    let udt_TwoDimArray                         = findUdt @"udt_TwoDimArray"
    let udt_InternalType                        = findUdt @"InternalType"
    let udt_NamedTuple                          = findUdt @"NamedTuple"

    let createTestContext op = globalContext.setCallable op


    let testOneFile fileName (expected:string) =
        let fullPath = Path.GetFullPath fileName
        let escapeCSharpString (s : string) = SymbolDisplay.FormatLiteral (s, false)
        let expected =
            expected
            |> (fun s -> s.Replace("%%%", fullPath |> HttpUtility.JavaScriptStringEncode |> escapeCSharpString))
            |> (fun s -> s.Replace("%%", fullPath |> escapeCSharpString))
        let tree = parse [Path.Combine ("Circuits", "Intrinsic.qs"); fileName]
        let actual =
            CodegenContext.Create (tree, ImmutableDictionary.Empty)
            |> generate (Path.GetFullPath fileName |> NonNullable<string>.New)
        Assert.Equal(expected |> clearFormatting, actual |> clearFormatting)

    let testOneBody (builder:SyntaxBuilder) (expected: string list) =
        let actual =
            builder.BuiltStatements
            |> List.map (fun s -> s.ToFullString())
        Assert.Equal(expected.Length, actual.Length)
        List.zip (expected |> List.map clearFormatting) (actual |> List.map clearFormatting) |> List.iter Assert.Equal

    let testOneList op (build: CodegenContext -> 'X -> 'Y List) (arg: 'X) (clean: 'Y -> 'Z) (expected: 'Z list) =
        let context = createTestContext op
        let actual =
            arg
            |> build context
            |> List.map clean

        List.zip expected actual
        |> List.iter Assert.Equal<'Z>

    [<Fact>]
    let ``tupleBaseClassName test`` () =
        let testOne (_, udt) expected =
            let context = (CodegenContext.Create syntaxTree).setUdt udt
            let actual = tupleBaseClassName context udt.Type
            Assert.Equal (expected |> clearFormatting, actual |> clearFormatting)

        "QTuple<IQArray<Qubit>>"
        |> testOne udt_args0

        "QTuple<(Int64, IQArray<Qubit>)>"
        |> testOne udt_args1

        "QTuple<ICallable>"
        |> testOne udt_A

        "QTuple<Microsoft.Quantum.Testing.A>"
        |> testOne udt_AA

        "QTuple<IUnitary>"
        |> testOne udt_U

        "QTuple<Qubit>"
        |> testOne udt_Q

        "QTuple<Double>"
        |> testOne udt_Real

        "QTuple<(Microsoft.Quantum.Testing.udt_Real,Microsoft.Quantum.Testing.udt_Real)>"
        |> testOne udt_Complex

        "QTuple<IQArray<IQArray<Result>>>"
        |> testOne udt_TwoDimArray

    [<Fact>]
    let ``operationDependencies test`` () =
        let NS1 = "Microsoft.Quantum.Testing"
        let NS2 = "Microsoft.Quantum.Intrinsic"
        let NSG = "Microsoft.Quantum.Compiler.Generics"
        let NSO = "Microsoft.Quantum.Overrides"
        let NSC = "Microsoft.Quantum.Core"

        let testOne (_,op) expected =
            let context = createTestContext op
            let sortByNames l = l |> List.sortBy (fun ((n,_),_) -> n) |> List.sortBy (fun ((_,ns),_) -> ns)
            let actual =
                op
                |> operationDependencies
                |> List.map (fun n -> ((n.Namespace.Value, n.Name.Value), (n |> roslynCallableTypeName context)))

            List.zip (expected |> sortByNames) (actual |> sortByNames)
            |> List.iter Assert.Equal

        []
        |> testOne emptyOperation

        []
        |> testOne oneQubitAbstractOperation

        [
            ((NS2, "CNOT"    ),  "IAdjointable<(Qubit,Qubit)>")
            ((NS2, "R"       ),  "IAdjointable<(Double,Qubit)>")
        ]
        |> testOne twoQubitOperation

        [
            ((NS1,"three_op1"),   "IUnitary<(Qubit,Qubit)>")
        ]
        |> testOne threeQubitOperation

        []
        |> testOne randomAbstractOperation

        [
            ((NS2, "Z"),                        "IUnitary<Qubit>")
            ((NS1, "selfInvokingOperation"),    "IAdjointable<Qubit>")
        ]
        |> testOne selfInvokingOperation

        [
            ((NSG, "genRecursion"),       "ICallable")
        ]
        |> testOne genRecursion

        [
            ((NS2, "M"       ),    "ICallable<Qubit, Result>")
            ((NS1, "let_f0"  ),    "ICallable<Int64, QRange>")
        ]
        |> testOne letsOperations

        []
        |> testOne helloWorld

        [
            ((NS2, "Allocate"    ),  "Allocate")
            ((NS2, "Borrow"      ),  "Borrow")
            ((NS2, "X"           ),  "IUnitary<Qubit>")
            ((NS2, "Z"           ),  "IUnitary<Qubit>")
            ((NS2, "Release"     ),  "Release")
            ((NS2, "Return"      ),  "Return")
            ((NS1, "alloc_op0"   ),  "ICallable<Qubit, QVoid>")
        ]
        |> testOne allocOperation

        []
        |> testOne failedOperation

        []
        |> testOne compareOps

        [
            ((NS1, "if_f0"),       "ICallable<QVoid, Int64>")
        ]
        |> testOne ifOperation

        [
            ((NSC, "RangeEnd"),    "ICallable<QRange, Int64>")
            ((NS1, "foreach_f2"),  "ICallable<(Int64,Int64), Int64>")
        ]
        |> testOne foreachOperation

        [
            ((NS2, "Allocate"),    "Allocate")
            ((NS2, "Release"),     "Release")
            ((NS1, "repeat_op0"),  "ICallable<Microsoft.Quantum.Testing.repeat_udt0, Result>")
            ((NS1, "repeat_op1"),  "ICallable<(Int64,IQArray<Qubit>), Result>")
            ((NS1, "repeat_op2"),  "ICallable<(Double,Microsoft.Quantum.Testing.repeat_udt0), Result>")
            ((NS1, "repeat_udt0"), "ICallable<(Int64,IQArray<Qubit>), Microsoft.Quantum.Testing.repeat_udt0>")
        ]
        |> testOne repeatOperation

        [
            ((NS1, "partial3Args"), "ICallable<(Int64,Double,Result), QVoid>")
            ((NS1, "partialFunction"), "ICallable<(Int64,Double,Pauli), Result>")
            ((NS1, "partialGeneric1"), "ICallable")
            ((NS1, "partialGeneric2"), "ICallable")
            ((NS1, "partialInnerTuple"), "ICallable<(Int64,(Double,Result)), QVoid>")
            ((NS1, "partialNestedArgsOp"), "ICallable<((Int64,Int64,Int64),((Double,Double),(Result,Result,Result))), QVoid>")
        ]
        |> testOne partialApplicationTest

        [
            ((NS1, "OP_1"),         "ICallable<Qubit, Result>")
        ]
        |> testOne opParametersTest

        [
            ((NS2, "Allocate"    ),  "Allocate")
            ((NS2, "Borrow"      ),  "Borrow")
            ((NSC, "Length"      ),  "ICallable")
            ((NS2, "Release"     ),  "Release")
            ((NS2, "Return"      ),  "Return")
            ((NS1, "random_f1"   ),  "ICallable<(Int64,Int64), Int64>")
            ((NS1, "random_op0"  ),  "ICallable<(Qubit,Int64), QVoid>")
            ((NS1, "random_op1"  ),  "ICallable<Qubit, Result>")
            ((NS1, "random_op10" ),  "ICallable<(Qubit,Int64), QVoid>")
            ((NS1, "random_op2"  ),  "ICallable<Qubit, Result>")
            ((NS1, "random_op3"  ),  "ICallable<(Qubit,Result,Pauli), QVoid>")
            ((NS1, "random_op4"  ),  "ICallable<(Qubit,Pauli), QVoid>")
            ((NS1, "random_op5"  ),  "IAdjointable<(Qubit,Pauli)>")
            ((NS1, "random_op6"  ),  "ICallable<(Qubit,Pauli), QVoid>")
            ((NS1, "random_op7"  ),  "IUnitary<(Qubit,Pauli)>")
            ((NS1, "random_op8"  ),  "ICallable<(Qubit,Pauli), QVoid>")
            ((NS1, "random_op9"  ),  "IUnitary<(Qubit,Pauli)>")
        ]
        |> testOne randomOperation

        [
            ((NS2, "Allocate"    ), "Allocate")
            ((NS2, "H"           ), "IUnitary<Qubit>")
            ((NSC, "Length"      ),  "ICallable")
            ((NS2, "M"           ), "ICallable<Qubit, Result>")
            ((NS2, "Release"     ), "Release")
            ((NS2, "S"           ), "IUnitary<Qubit>")
            ((NS1, "With1C"      ), "IControllable<(IAdjointable,IControllable,Qubit)>")
            ((NS2, "X"           ), "IUnitary<Qubit>")
        ]
        |> testOne measureWithScratch

        []
        |> testOne genC1

        [
            ((NSG, "genC2"          ), "ICallable")
        ]
        |> testOne genU1

        []
        |> testOne genCtrl3

        [
            ((NS2, "Allocate"       ), "Allocate")
            ((NS2, "CNOT"           ), "IAdjointable<(Qubit,Qubit)>")
            ((NS1, "Hold"           ), "ICallable")
            ((NS2, "Release"        ), "Release")
            ((NSG, "ResultToString" ), "ICallable<Result, String>")
            ((NS2, "X"              ), "IUnitary<Qubit>")
            ((NSG, "genIter"        ), "IUnitary")
            ((NSG, "genMapper"      ), "ICallable")
            ((NSG, "genU1"          ), "IUnitary")
            ((NS1, "noOpGeneric"    ), "IUnitary")
            ((NS1, "noOpResult"     ), "IUnitary<Result>")
        ]
        |> testOne usesGenerics

        [
            ((NSG, "genericWithMultipleTypeParams"), "ICallable")
        ]
        |> testOne callsGenericWithMultipleTypeParams

        [
            ((NS2, "Allocate"       ), "Allocate")
            ((NS2, "H"              ), "IUnitary<Qubit>")
            ((NS1, "H"              ), "ICallable<Qubit, QVoid>")
            ((NS2, "Release"        ), "Release")
            ((NS1, "emptyFunction"  ), "ICallable<QVoid, QVoid>")
            ((NSO, "emptyFunction"  ), "ICallable<QVoid, QVoid>")
        ]
        |> testOne duplicatedDefinitionsCaller

        [
            ((NS1, "iter"),         "ICallable")
            ((NSC, "Length"),       "ICallable")
        ]
        |> testOne testLengthDependency


    [<Fact>]
    let ``flatArgumentsList test`` () =
        let testOne (_, op: QsCallable) (expectedArgs: (string * string) list) =
            testOneList op flatArgumentsList op.ArgumentTuple id expectedArgs

        []
        |> testOne emptyOperation

        [
            ("n", "Int64")
        ]
        |> testOne helloWorld

        [
            ("q1", "Qubit")
        ]
        |> testOne oneQubitAbstractOperation

        [
            "q1", "Qubit"
            "t1", "(Qubit,Double)"
        ]
        |> testOne twoQubitOperation


        [
            "q1",   "Qubit"
            "q2",   "Qubit"
            "arr1", "Microsoft.Quantum.Intrinsic.Qubits"
        ]
        |> testOne threeQubitOperation

        [
            "q1", "Qubit"
            "b",  "Microsoft.Quantum.Testing.Basis"
            "t",  "(Pauli,IQArray<IQArray<Double>>,Boolean)"
            "i",  "Int64"
        ]
        |> testOne randomAbstractOperation

        [
            "a", "Int64"
            "b", "Int64"
            "c", "Double"
            "d", "Double"
        ]
        |> testOne nestedArgTuple1

        [
            "a", "(Int64,Int64)"
            "c", "Double"
            "b", "Int64"
            "d", "(Qubit,Qubit)"
            "e", "Double"
        ]
        |> testOne nestedArgTuple2

        [
            "outerOperation", "IAdjointable"
            "innerOperation", "IControllable"
            "target", "Qubit"
        ]
        |> testOne with1C

        [
            "a1", "__T__"
        ]
        |> testOne genC1

        [
            "arg1", "__X__"
            "arg2", "(Int64,(__Y__,__Z__),Result)"
        ]
        |> testOne genCtrl3

        [
            "second", "ICallable"
            "first",  "ICallable"
            "arg",    "__B__"
        ]
        |> testOne composeImpl

        [
            "mapper", "ICallable"
            "source", "IQArray<__T__>"
        ]
        |> testOne genMapper

    [<Fact>]
    let ``findQubitFields test`` () =
        let testOne (_,op) = testOneList op findQubitFields op.Signature.ArgumentType (snd >> formatSyntaxTree)

        []
        |> testOne emptyOperation

        []
        |> testOne helloWorld

        [
            "Data"
        ]
        |> testOne oneQubitAbstractOperation

        [
            "Data.Item1"
            "Data.Item2.Item1"
        ]
        |> testOne twoQubitOperation


        [
            "Data.Item1"
            "Data.Item2"
            "Data.Item3?.Data"
        ]
        |> testOne threeQubitOperation

        [
            "Data.Item1"
            "Data.Item2"
            "Data.Item3"
        ]
        |> testOne differentArgsOperation

        [
            "Data.Item1"
        ]
        |> testOne randomOperation

        [
            "Data.Item1"
        ]
        |> testOne randomAbstractOperation

        [
        ]
        |> testOne nestedArgTuple1

        [
            "Data.Item2.Item2.Item2.Item1"
            "Data.Item2.Item2.Item2.Item2"
        ]
        |> testOne nestedArgTuple2

        [
            "Data"
        ]
        |> testOne genU1

        [
            "Data.Item1"
            "Data.Item2.Item2.Item1"
            "Data.Item2.Item2.Item2"
        ]
        |> testOne genCtrl3

        [
            "Data.Item2?.Data.Item2"
            "Data.Item3?.Data.Item2.Item1"
            "Data.Item3?.Data.Item2.Item2"
            "Data.Item4"
        ]
        |> testOne udtTuple

        []
        |> testOne emptyFunction

        [
            "Data.Item2.Item1?.Data"
            "Data.Item2.Item2?.Data"
            "Data.Item3.Item1?.Data"
            "Data.Item3.Item2?.Data"
            "Data.Item4?.Data"
        ]
        |> testOne partialFunctionTest

    [<Fact>]
    let ``buildQubitsField test`` () =
        let testOne (_,op) expected = testOneList op buildQubitsField op.Signature.ArgumentType (formatSyntaxTree >> clearFormatting) (expected |> List.map clearFormatting)

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;"
        ]
        |> testOne emptyOperation

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;"
        ]
        |> testOne helloWorld

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    yield return Data;
                }
            }"
        ]
        |> testOne oneQubitAbstractOperation

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    yield return Data.Item1;
                    yield return Data.Item2.Item1;
                }
            }"
        ]
        |> testOne twoQubitOperation


        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    return Qubit.Concat(
                        ((IApplyData)Data.Item1)?.Qubits,
                        ((IApplyData)Data.Item2)?.Qubits,
                        ((IApplyData)Data.Item3?.Data)?.Qubits
                    );
                }
            }"
        ]
        |> testOne threeQubitOperation

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    return Qubit.Concat(
                        ((IApplyData)Data.Item1)?.Qubits,
                        ((IApplyData)Data.Item2)?.Qubits,
                        ((IApplyData)Data.Item3)?.Qubits
                    );
                }
            }"
        ]
        |> testOne differentArgsOperation

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    yield return Data.Item1;
                }
            }"
        ]
        |> testOne randomOperation

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;"
        ]
        |> testOne nestedArgTuple1

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    yield return Data.Item2.Item2.Item2.Item1;
                    yield return Data.Item2.Item2.Item2.Item2;
                }
            }"
        ]
        |> testOne nestedArgTuple2


        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    yield return Data.Item2?.Data.Item2;
                    yield return Data.Item3?.Data.Item2.Item1;
                    yield return Data.Item3?.Data.Item2.Item2;
                    yield return Data.Item4;
                }
            }"
        ]
        |> testOne udtTuple

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    return Qubit.Concat(
                        ((IApplyData)Data.Item1)?.Qubits,
                        ((IApplyData)Data.Item3.Item2?.Data.Item2)?.Qubits,
                        ((IApplyData)Data.Item3.Item3?.Data)?.Qubits,
                        ((IApplyData)Data.Item3.Item4?.Data)?.Qubits,
                        ((IApplyData)Data.Item3.Item5)?.Qubits
                    );
                }
            }"
        ]
        |> testOne letsOperations

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    var __temp1__ = Data;
                    return __temp1__?.GetQubits();
                }
            }"

        ]
        |> testOne genU1

        [
            "System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    var __temp1__ = Data.Item1;
                    var __temp2__ = Data.Item2.Item2.Item1;
                    var __temp3__ = Data.Item2.Item2.Item2;
                    return Qubit.Concat(
                        __temp1__?.GetQubits(),
                        __temp2__?.GetQubits(),
                        __temp3__?.GetQubits()
                    );
                }
            }"
        ]
        |> testOne genCtrl3

    [<Fact>]
    let ``areAllQubitArgs test`` () =
        let testOne (_,op) expected =
            let context = createTestContext op
            let actual =
                op.Signature.ArgumentType
                |> findQubitFields context
                |> List.map fst
                |> areAllQubitArgs
            Assert.Equal (expected, actual)

        true
        |> testOne emptyOperation

        true
        |> testOne helloWorld

        true
        |> testOne oneQubitAbstractOperation

        true
        |> testOne twoQubitOperation

        false
        |> testOne threeQubitOperation

        false
        |> testOne differentArgsOperation

        true
        |> testOne randomOperation

        true
        |> testOne randomAbstractOperation

        true
        |> testOne nestedArgTuple1

        true
        |> testOne nestedArgTuple2


    let depsByName (l : QsQualifiedName list) = l |> List.sortBy (fun n -> n.Namespace.Value) |> List.sortBy (fun n -> n.Name.Value)

    [<Fact>]
    let ``buildInit test`` () =
        let testOne (_,op) body =
            let context  = createTestContext op
            let deps     = op   |> operationDependencies |> depsByName
            let actual   = deps |> buildInit context |> formatSyntaxTree
            let expected = sprintf "public override void __Init__() { %s }" (String.concat "" body)
            Assert.Equal (expected |> clearFormatting, actual |> clearFormatting)

        let template = sprintf "this.__%s__ = this.Factory.Get<%s>(typeof(%s));"

        [
        ]
        |> testOne emptyOperation

        [
        ]
        |> testOne oneQubitAbstractOperation

        [
        ]
        |> testOne genU2

        [
            template "Microsoft_Quantum_Intrinsic_Allocate" "Allocate" "Microsoft.Quantum.Intrinsic.Allocate"
            template "Microsoft_Quantum_Intrinsic_H" "IUnitary<Qubit>" "Microsoft.Quantum.Intrinsic.H"
            template "Microsoft_Quantum_Testing_H" "ICallable<Qubit, QVoid>" "H"
            template "Microsoft_Quantum_Intrinsic_Release" "Release" "Microsoft.Quantum.Intrinsic.Release"
            template "Microsoft_Quantum_Overrides_emptyFunction" "ICallable<QVoid, QVoid>" "Microsoft.Quantum.Overrides.emptyFunction"
            template "Microsoft_Quantum_Testing_emptyFunction" "ICallable<QVoid, QVoid>" "emptyFunction"
        ]
        |> testOne duplicatedDefinitionsCaller

        [
            template "Microsoft_Quantum_Intrinsic_Allocate" "Allocate" "Microsoft.Quantum.Intrinsic.Allocate"
            template "Microsoft_Quantum_Intrinsic_CNOT" "IAdjointable<(Qubit, Qubit)>" "Microsoft.Quantum.Intrinsic.CNOT"
            template "Microsoft_Quantum_Testing_Hold" "ICallable" "Microsoft.Quantum.Testing.Hold<>"
            template "Microsoft_Quantum_Intrinsic_Release" "Release" "Microsoft.Quantum.Intrinsic.Release"
            template "Microsoft_Quantum_Compiler_Generics_ResultToString" "ICallable<Result,String>" "ResultToString"
            template "Microsoft_Quantum_Intrinsic_X" "IUnitary<Qubit>" "Microsoft.Quantum.Intrinsic.X"
            template "Microsoft_Quantum_Compiler_Generics_genIter" "IUnitary" "genIter<>"
            template "Microsoft_Quantum_Compiler_Generics_genMapper" "ICallable" "genMapper<,>"
            template "Microsoft_Quantum_Compiler_Generics_genU1" "IUnitary" "genU1<>"
            template "Microsoft_Quantum_Testing_noOpGeneric" "IUnitary" "Microsoft.Quantum.Testing.noOpGeneric<>"
            template "Microsoft_Quantum_Testing_noOpResult" "IUnitary<Result>" "Microsoft.Quantum.Testing.noOpResult"
        ]
        |> testOne usesGenerics

        [
            template "Microsoft_Quantum_Compiler_Generics_genericWithMultipleTypeParams" "ICallable" "genericWithMultipleTypeParams<,,>"
        ]
        |> testOne callsGenericWithMultipleTypeParams

        [
            template "Microsoft_Quantum_Intrinsic_Z" "IUnitary<Qubit>" "Microsoft.Quantum.Intrinsic.Z"
            "this.self = this;"
        ]
        |> testOne selfInvokingOperation

        [
            "this.self = this.Factory.Get<ICallable>(typeof(genRecursion<>));"
        ]
        |> testOne genRecursion

    [<Fact>]
    let ``getTypeOfOp test`` () =
        let testOne (_,op) =
            let dependendies context d =
                operationDependencies d
                |> List.map (getTypeOfOp context)
                |> List.map formatSyntaxTree
                |> List.sort
            testOneList op dependendies op id

        let template = sprintf "typeof(%s)"
        [
            template "Microsoft.Quantum.Intrinsic.Allocate"
            template "Microsoft.Quantum.Intrinsic.CNOT"
            template "Microsoft.Quantum.Testing.Hold<>"
            template "Microsoft.Quantum.Intrinsic.Release"
            template "ResultToString"
            template "Microsoft.Quantum.Intrinsic.X"
            template "genIter<>"
            template "genMapper<,>"
            template "genU1<>"
            template "Microsoft.Quantum.Testing.noOpGeneric<>"
            template "Microsoft.Quantum.Testing.noOpResult"
        ]
        |> List.sort
        |> testOne usesGenerics

        [
            template "genericWithMultipleTypeParams<,,>"
        ]
        |> testOne callsGenericWithMultipleTypeParams

        [
            template "composeImpl<,>"
        ]
        |> testOne compose

        [
            template "genRecursion<>"
        ]
        |> testOne genRecursion

        [
            template "Microsoft.Quantum.Intrinsic.Z"
            template "selfInvokingOperation"
        ]
        |> testOne selfInvokingOperation

    [<Fact>]
    let ``buildOpsProperties test`` () =
        let testOne (_,op) expected =
            let context = createTestContext op
            let actual =
                op
                |> operationDependencies
                |> depsByName
                |> buildOpsProperties context
                |> List.map formatSyntaxTree
            List.zip (expected |> List.map clearFormatting) (actual  |> List.map clearFormatting) |> List.iter Assert.Equal

        let template = sprintf @"protected %s __%s__ { get; set; }"

        [
            template "Allocate" "Microsoft_Quantum_Intrinsic_Allocate"
            template "IAdjointable<(Qubit,Qubit)>" "Microsoft_Quantum_Intrinsic_CNOT"
            template "ICallable" "Microsoft_Quantum_Testing_Hold"
            template "Release" "Microsoft_Quantum_Intrinsic_Release"
            template "ICallable<Result, String>" "Microsoft_Quantum_Compiler_Generics_ResultToString"
            template "IUnitary<Qubit>" "Microsoft_Quantum_Intrinsic_X"
            template "IUnitary" "Microsoft_Quantum_Compiler_Generics_genIter"
            template "ICallable" "Microsoft_Quantum_Compiler_Generics_genMapper"
            template "IUnitary" "Microsoft_Quantum_Compiler_Generics_genU1"
            template "IUnitary" "Microsoft_Quantum_Testing_noOpGeneric"
            template "IUnitary<Result>" "Microsoft_Quantum_Testing_noOpResult"
        ]
        |> testOne usesGenerics

        [
            template "ICallable" "Microsoft_Quantum_Compiler_Generics_genericWithMultipleTypeParams"
        ]
        |> testOne callsGenericWithMultipleTypeParams

        [
            template "IUnitary<Qubit>" "Microsoft_Quantum_Intrinsic_Z"
            "protected IAdjointable<Qubit> self { get; set; }"
        ]
        |> testOne selfInvokingOperation

        [
            "protected ICallable self { get; set; }"
        ]
        |> testOne genRecursion

    [<Fact>]
    let ``buildOperationInfoProperty test`` () =
        let testOne (_, op) expectedCodeString =
            let context = {createTestContext op with entryPoints = ImmutableArray.Create op.FullName}
            let (_, operationName) = findClassName context op
            let inType   = op.Signature.ArgumentType |> roslynTypeName context
            let outType  = op.Signature.ReturnType   |> roslynTypeName context
            let codeString =
                buildOperationInfoProperty context inType outType operationName
                |> formatSyntaxTree
            Assert.Equal(expectedCodeString |> clearFormatting, codeString |> clearFormatting)

        let template inType outType operationName =
            sprintf @"public static EntryPointInfo<%s, %s> Info => new EntryPointInfo<%s, %s>(typeof(%s));" inType outType inType outType operationName

        template "QVoid" "QVoid" "emptyOperation"
        |> testOne emptyOperation

        template "Qubit" "QVoid" "oneQubitAbstractOperation"
        |> testOne oneQubitAbstractOperation

        template "Qubit" "QVoid" "oneQubitOperation"
        |> testOne oneQubitOperation

        template "(Qubit,(Qubit,Double))" "QVoid" "twoQubitOperation"
        |> testOne twoQubitOperation

        template "(Qubit,Qubit,Microsoft.Quantum.Intrinsic.Qubits)" "QVoid" "threeQubitOperation"
        |> testOne threeQubitOperation

        template "(Qubit,Qubit,IQArray<Qubit>)" "QVoid" "differentArgsOperation"
        |> testOne differentArgsOperation

        template "Int64" "Int64" "helloWorld"
        |> testOne helloWorld

        template "(Qubit,ICallable,ICallable,IAdjointable,(IControllable,IUnitary),ICallable)" "ICallable" "opParametersTest"
        |> testOne opParametersTest

        template "QVoid" "QVoid" "emptyFunction"
        |> testOne emptyFunction

    let findBody op =
        let isBody (sp:QsSpecialization) = match sp.Kind with | QsBody -> true | _ -> false
        (op.Specializations |> Seq.find isBody)

    let findAdjoint op =
        let isAdjoint (sp:QsSpecialization) = match sp.Kind with | QsAdjoint -> true | _ -> false
        (op.Specializations |> Seq.find isAdjoint)

    let findControlled op =
        let isControlled (sp:QsSpecialization) = match sp.Kind with | QsControlled -> true | _ -> false
        (op.Specializations |> Seq.find isControlled)

    let findControlledAdjoint op =
        let isControlledAdjoint (sp:QsSpecialization) = match sp.Kind with | QsControlledAdjoint -> true | _ -> false
        (op.Specializations |> Seq.find isControlledAdjoint)

    let createVisitor (_,op) (sp:QsSpecialization) =
        let context = createTestContext op
        let builder = new SyntaxBuilder(context)
        builder.Namespaces.OnSpecializationDeclaration sp |> ignore
        builder

    let applyVisitor (ns,op) =
        createVisitor (ns,op) (findBody op)

    let adjointVisitor (ns,op) =
        createVisitor (ns,op) (findAdjoint op)

    let controlledVisitor (ns,op) =
        createVisitor (ns,op) (findControlled op)


    [<Fact>]
    let ``basic body builder`` () =
        let testOne = testOneBody

        [
        ]
        |> testOne (applyVisitor zeroQubitOperation)

        [
            "__Microsoft_Quantum_Intrinsic_X__.Apply(q1);"
        ]
        |> testOne (applyVisitor oneQubitOperation)

        [
            "__Microsoft_Quantum_Intrinsic_X__.Adjoint.Apply(q1);"
        ]
        |> testOne (adjointVisitor oneQubitOperation)

        [
            "var (q2, r) = t1;        "
            "__Microsoft_Quantum_Intrinsic_CNOT__.Apply((q1,q2));       "
            "__Microsoft_Quantum_Intrinsic_R__.Apply((r,q1));           "
        ]
        |> testOne (applyVisitor twoQubitOperation)
        [
            "var (q2, r) = t1;        "
            "__Microsoft_Quantum_Intrinsic_R__.Adjoint.Apply((r,q1));"
            "__Microsoft_Quantum_Intrinsic_CNOT__.Adjoint.Apply((q1,q2));"
        ]
        |> testOne (adjointVisitor twoQubitOperation)

        [
            "__Microsoft_Quantum_Testing_three__op1__.Apply((q1,q2));"
            "__Microsoft_Quantum_Testing_three__op1__.Apply((q2,q1));"
            "__Microsoft_Quantum_Testing_three__op1__.Apply((q1,q2));"
        ]
        |> testOne (applyVisitor threeQubitOperation)

        [
            "__Microsoft_Quantum_Intrinsic_Z__.Adjoint.Apply(q1);"
            "self.Apply(q1);"
        ]
        |> testOne (adjointVisitor selfInvokingOperation)

    [<Fact>]
    let ``recursive functions body`` () =
        let testOne = testOneBody

        [
            """
            if ((cnt == 0L))
            {
                return x;
            }
            else
            {
                return self.Apply<__T__>((x, (cnt - 1L)));
            }
            """
        ]
        |> testOne (applyVisitor genRecursion)

        [
            """
            if ((x == 1L))
            {
                return 1L;
            }
            else
            {
                return (x * self.Apply<Int64>((x - 1L)));
            }
            """
        ]
        |> testOne (applyVisitor factorial)

    [<Fact>]
    let ``generic functions body`` () =
        let testOne = testOneBody

        [
            "__Microsoft_Quantum_Intrinsic_X__.Apply(q1);"
        ]
        |> testOne (applyVisitor oneQubitOperation)

    [<Fact>]
    let ``composed generic  body`` () =
        let testOne = testOneBody

        [
            "second.Apply(first.Apply<__A__>(arg));"
        ]
        |> testOne (applyVisitor composeImpl)

        [
            "return __Microsoft_Quantum_Compiler_Generics_composeImpl__.Partial((second, first, _));"
        ]
        |> testOne (applyVisitor compose)


    [<Fact>]
    let ``usesGenerics body`` () =
        [
            "var a = (IQArray<Result>)new QArray<Result>(Result.One, Result.Zero, Result.Zero);"
            "var s = (IQArray<String>)new QArray<String>(__Microsoft_Quantum_Compiler_Generics_ResultToString__.Apply(a[0L]), __Microsoft_Quantum_Compiler_Generics_ResultToString__.Apply(a[1L]));"
            "__Microsoft_Quantum_Testing_noOpResult__.Apply(a[0L]);"

            """
            {
                var qubits = __Microsoft_Quantum_Intrinsic_Allocate__.Apply(3L);
#line hidden
                bool __arg1__ = true;
                try
                {
                    var op = __Microsoft_Quantum_Testing_Hold__.Partial(new Func<QVoid,(ICallable,(Qubit,Qubit),QVoid)>((__arg2__) => (__Microsoft_Quantum_Intrinsic_CNOT__, (qubits[0L], qubits[1L]), __arg2__)));
                    op.Apply(QVoid.Instance);

                    __Microsoft_Quantum_Testing_noOpGeneric__.Apply(qubits[0L]);
                    __Microsoft_Quantum_Testing_noOpGeneric__.Apply(a[0L]);
                    __Microsoft_Quantum_Compiler_Generics_genIter__.Apply((__Microsoft_Quantum_Intrinsic_X__, qubits));
                }
#line hidden
                catch
                {
                    __arg1__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg1__)
                    {
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(qubits);
                    }
                }
            }
            """
            "__Microsoft_Quantum_Compiler_Generics_genIter__.Apply((__Microsoft_Quantum_Testing_noOpResult__, a));"
            """
            __Microsoft_Quantum_Compiler_Generics_genIter__.Apply((__Microsoft_Quantum_Compiler_Generics_genU1__, __Microsoft_Quantum_Compiler_Generics_genMapper__.Apply<IQArray<String>>((__Microsoft_Quantum_Compiler_Generics_ResultToString__, a))));
            """
            "__Microsoft_Quantum_Compiler_Generics_genIter__.Apply((__Microsoft_Quantum_Compiler_Generics_genU1__, s));"
            "__Microsoft_Quantum_Compiler_Generics_genIter__.Apply((__Microsoft_Quantum_Compiler_Generics_genU1__, a));"
        ]
        |> testOneBody (applyVisitor usesGenerics)


    [<Fact>]
    let ``callTests body`` () =
        [
            "var plain = new Microsoft.Quantum.Testing.call_plain(__Microsoft_Quantum_Intrinsic_X__);"
            "var adj   = new Microsoft.Quantum.Testing.call_adj(__Microsoft_Quantum_Intrinsic_X__);"
            "var ctr   = new Microsoft.Quantum.Testing.call_ctr(__Microsoft_Quantum_Intrinsic_X__);"
            "var uni   = new Microsoft.Quantum.Testing.call_uni(__Microsoft_Quantum_Intrinsic_X__);"

            "__Microsoft_Quantum_Intrinsic_X__.Apply(qubits.Data[0L]);"
            "__Microsoft_Quantum_Intrinsic_X__.Adjoint.Apply(qubits.Data[0L]);"
            "__Microsoft_Quantum_Intrinsic_X__.Controlled.Apply((qubits.Data?.Slice(new QRange(1L,5L)), qubits.Data[0L]));"

            "__Microsoft_Quantum_Testing_call__target1__.Apply((1L, __Microsoft_Quantum_Intrinsic_X__,     __Microsoft_Quantum_Intrinsic_X__,   __Microsoft_Quantum_Intrinsic_X__,   __Microsoft_Quantum_Intrinsic_X__));"
            "__Microsoft_Quantum_Testing_call__target1__.Apply((1L, plain.Data, adj.Data, ctr.Data, uni.Data));"

            "__Microsoft_Quantum_Testing_call__target2__.Apply((1L, (Result.Zero, __Microsoft_Quantum_Intrinsic_X__),    (Result.Zero, __Microsoft_Quantum_Intrinsic_X__),  (Result.Zero, __Microsoft_Quantum_Intrinsic_X__),  (Result.Zero, __Microsoft_Quantum_Intrinsic_X__)));"
            "__Microsoft_Quantum_Testing_call__target2__.Apply((2L, (Result.One, plain.Data), (Result.One, adj.Data), (Result.One, ctr.Data), (Result.One, uni.Data)));"
        ]
        |> testOneBody (applyVisitor callTests)


    [<Fact>]
    let ``lets operations`` () =
        [
            "var q2 = q1;"

            "var r = __Microsoft_Quantum_Intrinsic_M__.Apply(q1);"

            "var i = 1.1D;"
            "var iZero = 0L;"
            "var dZero = 0D;"

            "var a = new QRange(0L, 10L);"
            "var b = new QRange(8L, -(1L), 5L);"
            "var j = (n + 1L);"
            "var k = ((((n - 1L) * n.Pow(2L)) / 3L) % 4L);"

            "var t = (2.2D, (3L, Result.One));"
            "var (l,(m,o)) = t;             "
            "var (p,q) = t;                 "
            "var (u0,u1,u2,u3,call1) = udts;"
            "var u = u3.Data.Apply<Microsoft.Quantum.Testing.let_udt_2>(u1);"
            """
            if (true)
            {
                var (l2,(m2,o2)) = t;
                return u3.Data.Apply<Microsoft.Quantum.Testing.let_udt_2>(u1).Data.Apply<QRange>(u1);
            }
            """
            "var s = String.Format(\"n is {0} and u is {1}, {2}, {3}, {4}\",n,u3.Data.Apply<Microsoft.Quantum.Testing.let_udt_2>(u1),r,n,j);"
            "var str = String.Format(\"Hello{0} world!\", (true ? \" quantum\" : \"\"));"

            "var (l3,_) = t;"
            "var (_,(_,o3)) = t;"
            "var (_,(m3,_)) = t;"

            "var __arg1__ = t;"
            "var __arg2__ = t;"

            "return __Microsoft_Quantum_Testing_let__f0__.Apply(n);"
        ]
        |> testOneBody (applyVisitor letsOperations)

    [<Fact>]
    let ``bit operations`` () =
        [
            "var andEx    = (a & b);  "
            "var orEx     = (a | b);  "
            "var xorEx    = (a ^ b);  "
            "var left     = (a << (int)b); "
            "var right    = (a >> (int)b); "
            "var negation = ~(a);       "

            "var total = (((((andEx + orEx) + xorEx) + left) + right) + negation);"
            """
            if ((total > 0L))
            {
                return true;
            }
            else
            {
                return false;
            }
            """
        ]
        |> testOneBody (applyVisitor bitOperations)

    [<Fact>]
    let ``helloWorld body`` () =
        [
            "var r = (n + 1L);"
            "return r;"
        ]
        |> testOneBody (applyVisitor helloWorld)

    [<Fact>]
    let ``if operations`` () =
        [
            "var n = 0L;"
            """
            if ((r == Result.One))
            {
                n = (__Microsoft_Quantum_Testing_if__f0__.Apply(QVoid.Instance) * i);
            }
            """
            """
            if ((p == Pauli.PauliX))
            {
                return n;
            }
            else
            {
                return 0L;
            }
            """
            """
            if ((p == Pauli.PauliX))
            {
                return n;
            }
            else if ((p == Pauli.PauliY))
            {
                return 1L;
            }
            else
            {
                return ((p==Pauli.PauliI)?3L:__Microsoft_Quantum_Testing_if__f0__.Apply(QVoid.Instance));
            }
            """
        ]
        |> testOneBody (applyVisitor ifOperation)

    [<Fact>]
    let ``foreach operations`` () =
        [
            "var result = 0L;"
            @"foreach (var n in new QRange(0L, i))
            #line hidden
            {
                result = (result + i);
            }"
            @"foreach (var n in new QRange(i, -(1L), 0L))
            #line hidden
            {
                result = ((result - i) * 2L);
            }"
            "var range = new QRange(0L, 10L);"
            @"foreach (var n in range)
            #line hidden
            {
                result = ((range.End + result) + (n * -(__Microsoft_Quantum_Testing_foreach__f2__.Apply((n, 4L)))));
            }"
            """
            if ((result > 10L))
            {
                return Result.One;
            }
            else
            {
                return Result.Zero;
            }"""
        ]
        |> testOneBody (applyVisitor foreachOperation)

    [<Fact>]
    let ``udt operations`` () =
        [
            "var args0 = new Microsoft.Quantum.Testing.udt_args0(qubits);"
            "var args1 = new Microsoft.Quantum.Testing.udt_args1((1L, args0.Data));"
            "var args1a = op2.Apply<Microsoft.Quantum.Testing.udt_args1>(args0);"
            "var args2 = new Microsoft.Quantum.Testing.udt_args2(op2);"
            "var args3 = new Microsoft.Quantum.Testing.udt_args3(op2);"
            "var args4 = new Microsoft.Quantum.Testing.udt_args4(op2);"
            "var ext0  = new Microsoft.Quantum.Overrides.udt0((Result.Zero, Result.One));"
            "op0.Apply(args0);"
            "op0.Apply(new Microsoft.Quantum.Testing.udt_args0(qubits));"
            "op1.Apply(args1);"
            "op1.Apply(new Microsoft.Quantum.Testing.udt_args1((2L, qubits)));"
            "op1.Apply(new Microsoft.Quantum.Testing.udt_args1((3L, args0.Data)));"
            "op1.Apply(new Microsoft.Quantum.Testing.udt_args1((4L, new Microsoft.Quantum.Testing.udt_args0(qubits).Data)));"

            "return new Microsoft.Quantum.Testing.udt_args1((22L, qubits));"
        ]
        |> testOneBody (applyVisitor udtsTest)

    [<Fact>]
    let ``test Length dependency`` () =
        [
            "__Microsoft_Quantum_Testing_iter__.Apply((__Microsoft_Quantum_Core_Length__, new QArray<IQArray<Result>>(new QArray<Result>(Result.One), new QArray<Result>(Result.Zero, Result.One))));"
        ]
        |> testOneBody (applyVisitor testLengthDependency)

    [<Fact>]
    let ``udt return values`` () =
        [
            "return QVoid.Instance;"
        ]
        |> testOneBody (applyVisitor returnTest1)

        [
            "return 5L;"
        ]
        |> testOneBody (applyVisitor returnTest2)

        [
            "return (5L, 6L);"
        ]
        |> testOneBody (applyVisitor returnTest3)

        [
            "return new Microsoft.Quantum.Testing.returnUdt0((7L, 8L));"
        ]
        |> testOneBody (applyVisitor returnTest4)

        [
            "return new QArray<Int64>(9L, 10L);"
        ]
        |> testOneBody (applyVisitor returnTest5)

        [
            "return new Microsoft.Quantum.Testing.returnUdt1( new QArray<(Int64,Int64)>((1L, 2L), (3L, 4L)));"
        ]
        |> testOneBody (applyVisitor returnTest6)

        [
            "return new QArray<Microsoft.Quantum.Testing.returnUdt0>( new Microsoft.Quantum.Testing.returnUdt0((1L, 2L)), new Microsoft.Quantum.Testing.returnUdt0((3L, 4L)));"
        ]
        |> testOneBody (applyVisitor returnTest7)

        [
            "return new Microsoft.Quantum.Testing.returnUdt3(new QArray<Microsoft.Quantum.Testing.returnUdt0>(new Microsoft.Quantum.Testing.returnUdt0((1L, 2L)), new Microsoft.Quantum.Testing.returnUdt0((3L, 4L))));"
        ]
        |> testOneBody (applyVisitor returnTest8)

        [
            "return (new Microsoft.Quantum.Testing.returnUdt0((7L, 8L)), new Microsoft.Quantum.Testing.returnUdt1(new QArray<(Int64,Int64)>((1L, 2L), (3L, 4L))));"
        ]
        |> testOneBody (applyVisitor returnTest9)

        [
            "return new Microsoft.Quantum.Overrides.udt0((Result.Zero, Result.One));"
        ]
        |> testOneBody (applyVisitor returnTest10)

    [<Fact>]
    let ``repeat operation`` () =
        [
            """
            {
                var qubits = __Microsoft_Quantum_Intrinsic_Allocate__.Apply(i);
#line hidden
                bool __arg1__ = true;
                try
                {
                    while (true)
                    {
                        var res =  __Microsoft_Quantum_Testing_repeat__op0__.Apply(new Microsoft.Quantum.Testing.repeat_udt0((0L, qubits)));

                        if ((__Microsoft_Quantum_Testing_repeat__op1__.Apply((0L, qubits)) == Result.One))
                        {
                            break;
                        }
                        else
                        {
                            res = __Microsoft_Quantum_Testing_repeat__op2__.Apply((3D, new Microsoft.Quantum.Testing.repeat_udt0(((i-1L), qubits))));
                        }
                    }
                }
#line hidden
                catch
                {
                    __arg1__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg1__)
                    {
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(qubits);
                    }
                }
            }
            """
        ]
        |> testOneBody (applyVisitor repeatOperation)

    [<Fact>]
    let ``allocate operations`` () =
        [
            """
            {
                var q = __Microsoft_Quantum_Intrinsic_Allocate__.Apply();
#line hidden
                bool __arg1__ = true;
                try
                {
                    var flag = true;
                    (flag ? __Microsoft_Quantum_Intrinsic_X__ : __Microsoft_Quantum_Intrinsic_Z__).Apply(q);
                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(q);
                }
#line hidden
                catch
                {
                    __arg1__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg1__)
                    {
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q);
                    }
                }
            }"""
            """
            {
                var qs = __Microsoft_Quantum_Intrinsic_Allocate__.Apply(n);
#line hidden
                bool __arg2__ = true;
                try
                {
                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(qs[(n-1L)]);
                }
#line hidden
                catch
                {
                    __arg2__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg2__)
                    {
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(qs);
                    }
                }
            }"""
            """
            {
                var (q1, (q2, (__arg3__, q3, __arg4__, q4))) = (__Microsoft_Quantum_Intrinsic_Allocate__.Apply(), ((__Microsoft_Quantum_Intrinsic_Allocate__.Apply(), __Microsoft_Quantum_Intrinsic_Allocate__.Apply(2L)), (__Microsoft_Quantum_Intrinsic_Allocate__.Apply(), __Microsoft_Quantum_Intrinsic_Allocate__.Apply(n), __Microsoft_Quantum_Intrinsic_Allocate__.Apply((n-1L)), __Microsoft_Quantum_Intrinsic_Allocate__.Apply(4L))));
#line hidden
                bool __arg5__ = true;
                try
                {
                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(q1);
                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(q3[1L]);
                }
#line hidden
                catch
                {
                    __arg5__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg5__)
                    {
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q1);
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q2.Item1);
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q2.Item2);
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(__arg3__);
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q3);
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(__arg4__);
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q4);
                    }
                }
            }"""
        ]
        |> testOneBody (applyVisitor allocOperation)

        [
            """
            {
                var b = __Microsoft_Quantum_Intrinsic_Borrow__.Apply(n);
#line hidden
                bool __arg1__ = true;
                try
                {
                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(b[(n-1L)]);
                }
#line hidden
                catch
                {
                    __arg1__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg1__)
                    {
                        __Microsoft_Quantum_Intrinsic_Return__.Apply(b);
                    }
                }
            }"""
            """
            {
                var (q1, (q2, (__arg2__, q3))) = (__Microsoft_Quantum_Intrinsic_Borrow__.Apply(), (__Microsoft_Quantum_Intrinsic_Borrow__.Apply(2L), (__Microsoft_Quantum_Intrinsic_Borrow__.Apply(), (__Microsoft_Quantum_Intrinsic_Borrow__.Apply(n), __Microsoft_Quantum_Intrinsic_Borrow__.Apply(4L)))));
#line hidden
                bool __arg3__ = true;
                try
                {
                    {
                        var qt = (__Microsoft_Quantum_Intrinsic_Allocate__.Apply(), (__Microsoft_Quantum_Intrinsic_Allocate__.Apply(1L), __Microsoft_Quantum_Intrinsic_Allocate__.Apply(2L)));
#line hidden
                        bool __arg4__ = true;
                        try
                        {
                            var (qt1, qt2) = ((Qubit, (IQArray<Qubit>, IQArray<Qubit>)))qt;
                            __Microsoft_Quantum_Testing_alloc__op0__.Apply(qt1);
                        }
#line hidden
                        catch
                        {
                            __arg4__ = false;
                            throw;
                        }
#line hidden
                        finally
                        {
                            if (__arg4__)
                            {
                                __Microsoft_Quantum_Intrinsic_Release__.Apply(qt.Item1);
                                __Microsoft_Quantum_Intrinsic_Release__.Apply(qt.Item2.Item1);
                                __Microsoft_Quantum_Intrinsic_Release__.Apply(qt.Item2.Item2);
                            }
                        }
                    }

                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(q1);
                    __Microsoft_Quantum_Testing_alloc__op0__.Apply(q2[1L]);
                }
#line hidden
                catch
                {
                    __arg3__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg3__)
                    {
                        __Microsoft_Quantum_Intrinsic_Return__.Apply(q1);
                        __Microsoft_Quantum_Intrinsic_Return__.Apply(q2);
                        __Microsoft_Quantum_Intrinsic_Return__.Apply(__arg2__);
                        __Microsoft_Quantum_Intrinsic_Return__.Apply(q3.Item1);
                        __Microsoft_Quantum_Intrinsic_Return__.Apply(q3.Item2);
                    }
                }
            }"""
        ]
        |> testOneBody (adjointVisitor allocOperation)

    [<Fact>]
    let ``failed operation`` () =
        [
            @"throw new ExecutionFailException(""This operation should never be called."");"
            @"return 1L;"
        ]
        |> testOneBody (applyVisitor failedOperation)


    [<Fact>]
    let ``compare operations`` () =
        [
            "var lt = (n < 1L);"
            "var lte = (n <= 2L);"
            "var gt = (n > 3L);"
            "var gte = (n >= 4L);"
            "return (((lt == (lte && gt)) != gte) || !(lt));"
        ]
        |> testOneBody (applyVisitor compareOps)

    let testOneSpecialization pick (_,op) expected =
        let context = createTestContext op
        let actual  = op |> pick |> buildSpecialization context |> Option.map (fst >> formatSyntaxTree)
        Assert.Equal(expected |> Option.map clearFormatting, actual |> Option.map clearFormatting)

    [<Fact>]
    let ``buildSpecialization - apply`` () =
        let testOne = testOneSpecialization findBody

        None
        |> testOne emptyOperation

        None
        |> testOne oneQubitAbstractOperation

        None
        |> testOne oneQubitSelfAdjointAbstractOperation

        None
        |> testOne randomAbstractOperation

        Some """
        public override Func<QVoid,QVoid> __Body__ => (__in__) =>
        {
            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne zeroQubitOperation

        Some """
        public override Func<Qubit,QVoid> __Body__ => (__in__) =>
        {
            var q1 = __in__;

            __Microsoft_Quantum_Intrinsic_X__.Apply(q1);
            #line hidden
            return  QVoid.Instance;
        };
        """
        |> testOne oneQubitOperation

        Some """
        public override Func<(Qubit,(Qubit,Double)), QVoid> __Body__ => (__in__) =>
        {
            var (q1,t1) = __in__;

            var (q2,r) = t1;

            __Microsoft_Quantum_Intrinsic_CNOT__.Apply((q1, q2));
            __Microsoft_Quantum_Intrinsic_R__.Apply((r, q1));

            #line hidden
            return  QVoid.Instance;
        };
        """
        |> testOne twoQubitOperation


        Some """
        public override Func<(Qubit,Qubit,IQArray<Qubit>), QVoid> __Body__ => (__in__) =>
        {
            var (q1,q2,arr1) = __in__;

            __Microsoft_Quantum_Testing_da__op0__.Apply(QVoid.Instance);
            __Microsoft_Quantum_Testing_da__op1__.Adjoint.Apply(q1);
            __Microsoft_Quantum_Testing_da__op2__.Controlled.Apply((new QArray<Qubit>(q1), (1L, q2)));
            __Microsoft_Quantum_Testing_da__op3__.Controlled.Adjoint.Apply((new QArray<Qubit>(q1, q2), (1.1D, Result.One, arr1.Length)));

            #line hidden
            return QVoid.Instance;
        };
        """
        |> testOne differentArgsOperation

    [<Fact>]
    let ``operation/function types`` () =
        let testOne = testOneSpecialization findBody

        let ret = "ICallable";
        let op0 = "ICallable";
        let op1 = "ICallable";
        let op2 = "IAdjointable";
        let op3 = "IControllable";
        let op4 = "IUnitary";
        let f1  = "ICallable"
        Some (sprintf """
        public override Func<(Qubit, %s, %s, %s, (%s, %s), %s), %s> __Body__  => (__in__) =>
                {
                    var (q1, op0, op1, op2, t1, f1) = __in__;
                    op1.Apply(__Microsoft_Quantum_Testing_OP__1__);
                    var v0 = op0;
                    var r0 = v0.Apply<Result>(q1);
                    var (op3, op4) = t1;
                    op3.Apply((new QArray<Qubit>(q1), (q1, q1)));

                    return op2.Partial(new Func<Qubit, (Qubit,Qubit)>((__arg1__) => (q1, __arg1__)));
        };"""  op0 op1 op2 op3 op4 f1 ret)
        |> testOne opParametersTest

    [<Fact>]
    let ``array operations`` () =
        [
            "var q = (IQArray<Qubit>)qubits;"
            "var r1 = (IQArray<Result>)new QArray<Result>(Result.Zero);"
            "var r2 = (IQArray<Int64>)new QArray<Int64>(0L, 1L);"
            "var r3 = (IQArray<Double>)new QArray<Double>(0D, 1.1D, 2.2D);"
            "var r4 = (IQArray<Int64>)new QArray<Int64>(r2[0L], r2[1L], 2L);"
            "var r5 = (IQArray<Result>)QArray<Result>.Create((4L + 2L));"
            "var r6 = QArray<Pauli>.Create(r5.Length);"
            "var r7 = (IQArray<Int64>)QArray<Int64>.Add(r2, r4);"
            "var r8 = (IQArray<Int64>)r7?.Slice(new QRange(1L, 5L, 10L));"

            "var r9 = new Microsoft.Quantum.Testing.arrays_T1(new QArray<Pauli>(Pauli.PauliX, Pauli.PauliY));"
            "var r10 = (IQArray<Microsoft.Quantum.Testing.arrays_T1>)QArray<Microsoft.Quantum.Testing.arrays_T1>.Create(4L);"
            "var r11 = new Microsoft.Quantum.Testing.arrays_T2((new QArray<Pauli>(Pauli.PauliZ), new QArray<Int64>(4L)));"
            "var r12 = (IQArray<Microsoft.Quantum.Testing.arrays_T2>)QArray<Microsoft.Quantum.Testing.arrays_T2>.Create(r10.Length);"
            "var r13 = new Microsoft.Quantum.Testing.arrays_T3(new QArray<IQArray<Result>>(new QArray<Result>(Result.Zero, Result.One), new QArray<Result>(Result.One, Result.Zero)));"
            "var r14 = (IQArray<Qubit>)QArray<Qubit>.Add(qubits, register.Data);"
            "var r15 = (IQArray<Qubit>)register.Data?.Slice(new QRange(0L, 2L));"
            "var r16 = (IQArray<Qubit>)qubits?.Slice(new QRange(1L, -(1L)));"
            "var r18 = (IQArray<Microsoft.Quantum.Intrinsic.Qubits>)QArray<Microsoft.Quantum.Intrinsic.Qubits>.Create(2L);"
            "var r19 = (IQArray<Microsoft.Quantum.Overrides.udt0>)QArray<Microsoft.Quantum.Overrides.udt0>.Create(7L);"
            "var i0 = r13.Data[0L][1L];"
            "var i1 = r2[(0L + r1.Length)];"
            "var i2 = r3[(i1 * ((2L + 3L) - (8L % 1L)))];"
            "var i3 = qubits[0L];"
            "var i4 = (IQArray<QRange>)indices[0L];"
            "var i5 = indices[0L][1L];"
            "var i6 = (IQArray<Result>)t.Data[0L];"
            "var i7 = register.Data[3L];"

            "var l0 = qubits.Length;"
            "var l1 = indices.Length;"
            "var l2 = indices[0L].Length;"
            "var l3 = t.Data.Length;"
            "var l4 = r8.Length;"
            "var l5 = r9.Data.Length;"
            "var l6 = register.Data.Length;"

            "return new QArray<IQArray<Result>>(new QArray<Result>(i0, Result.One), new QArray<Result>(Result.Zero));"
        ]
        |> testOneBody (applyVisitor arraysOperations)


    [<Fact>]
    let ``array slice`` () =
        [
            "var r2 = new QRange(10L,-(2L),0L);"
            "var ranges = (IQArray<QRange>)QArray<QRange>.Create(1L);"

            "var s1 = (IQArray<Qubit>)qubits?.Slice(new QRange(0L,10L));"
            "var s2 = (IQArray<Qubit>)qubits?.Slice(r2);"
            "var s3 = (IQArray<Qubit>)qubits?.Slice(ranges[3L]);"
            "var s4 = (IQArray<Qubit>)qubits?.Slice(__Microsoft_Quantum_Testing_GetMeARange__.Apply(QVoid.Instance));"

            "return qubits?.Slice(new QRange(10L,-(3L),0L));"
        ]
        |> testOneBody (applyVisitor sliceOperations)

    [<Fact>]
    let ``range operations`` () =
        [
            "return ((r.Start + r.End) + r.Step);"
        ]
        |> testOneBody (applyVisitor rangeOperations)

    [<Fact>]
    let ``generic parameter types`` () =
        let testOne (ns,op : QsCallable) (expected: string list) =
            let actual =
                op.Signature
                |> typeParametersNames
            List.zip (expected |> List.map clearFormatting) (actual |> List.map clearFormatting)
            |> List.iter Assert.Equal

        []
        |> testOne emptyOperation

        []
        |> testOne oneQubitAbstractOperation

        []
        |> testOne randomAbstractOperation

        [
            "__T__"
        ]
        |> testOne genC1

        [
            "__T__"
            "__U__"
        ]
        |> testOne genC2

        [
            "__X__"
            "__Y__"
            "__Z__"
        ]
        |> testOne genCtrl3

        [
            "__T__"
            "__U__"
        ]
        |> testOne genMapper

    [<Fact>]
    let ``buildSpecialization - adjoint`` () =
        let testOne = testOneSpecialization findAdjoint

        None
        |> testOne oneQubitAbstractOperation

        Some "public override Func<Qubit, QVoid> __AdjointBody__ => __Body__;"
        |> testOne oneQubitSelfAdjointAbstractOperation

        None
        |> testOne randomAbstractOperation

        Some "public override Func<Qubit, QVoid> __AdjointBody__ => __Body__;"
        |> testOne oneQubitSelfAdjointOperation

        Some """
        public override Func<QVoid, QVoid> __AdjointBody__ => (__in__) =>
        {
            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne zeroQubitOperation

        Some """
        public override Func<Qubit, QVoid> __AdjointBody__ => (__in__) =>
        {
            var q1 = __in__;
            __Microsoft_Quantum_Intrinsic_X__.Adjoint.Apply(q1);

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne oneQubitOperation

        Some """
        public override Func<(Qubit,(Qubit,Double)), QVoid> __AdjointBody__ => (__in__) =>
        {
            var (q1,t1) = __in__;

            var (q2,r) = t1;

            __Microsoft_Quantum_Intrinsic_R__.Adjoint.Apply((r, q1));
            __Microsoft_Quantum_Intrinsic_CNOT__.Adjoint.Apply((q1, q2));

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne twoQubitOperation

        Some """
        public override Func<(Qubit,Qubit,Microsoft.Quantum.Intrinsic.Qubits), QVoid> __AdjointBody__ => (__in__) =>
        {
            var (q1,q2,arr1) = __in__;
            __Microsoft_Quantum_Testing_three__op1__.Adjoint.Apply((q1, q2));
            __Microsoft_Quantum_Testing_three__op1__.Adjoint.Apply((q2, q1));
            __Microsoft_Quantum_Testing_three__op1__.Adjoint.Apply((q1, q2));
            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne threeQubitOperation


        Some "public override Func<__T__, QVoid> __AdjointBody__ => __Body__;"
        |> testOne genAdj1

    [<Fact>]
    let ``buildSpecialization - controlled`` () =
        let testOne = testOneSpecialization findControlled

        None
        |> testOne oneQubitAbstractOperation

        None
        |> testOne oneQubitSelfAdjointAbstractOperation

        None
        |> testOne randomAbstractOperation

        Some """
        public override Func<(IQArray<Qubit>,QVoid), QVoid> __ControlledBody__ => (__in__) =>
        {
            var (__controlQubits__, __unitArg__) = __in__;

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne zeroQubitOperation

        Some """
        public override Func<(IQArray<Qubit>,Qubit), QVoid> __ControlledBody__ => (__in__) =>
        {
            var (c, q1) = __in__;

            __Microsoft_Quantum_Intrinsic_X__.Controlled.Apply((c, q1));

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne oneQubitOperation

        Some """
        public override Func<(IQArray<Qubit>,(Qubit,Qubit,Microsoft.Quantum.Intrinsic.Qubits)), QVoid> __ControlledBody__ => (__in__) =>
        {
            var (c, (q1, q2, arr1)) = __in__;

            __Microsoft_Quantum_Testing_three__op1__.Controlled.Apply((c, (q1, q2)));
            __Microsoft_Quantum_Testing_three__op1__.Controlled.Apply((c, (q2, q1)));
            __Microsoft_Quantum_Testing_three__op1__.Controlled.Apply((c, (q1, q2)));

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne threeQubitOperation

    [<Fact>]
    let ``buildSpecialization - controlled-adjoint`` () =
        let testOne = testOneSpecialization findControlledAdjoint

        None
        |> testOne oneQubitAbstractOperation

        Some "public override Func<(IQArray<Qubit>,Qubit), QVoid> __ControlledAdjointBody__ => __ControlledBody__;"
        |> testOne oneQubitSelfAdjointAbstractOperation

        None
        |> testOne randomAbstractOperation

        Some """
        public override Func<(IQArray<Qubit>,QVoid), QVoid> __ControlledAdjointBody__ => (__in__) =>
        {
            var (__controlQubits__, __unitArg__) = __in__;

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne zeroQubitOperation

        Some """
        public override Func<(IQArray<Qubit>, Qubit), QVoid> __ControlledAdjointBody__ => (__in__) =>
        {
            var (c,q1) = __in__;
            __Microsoft_Quantum_Intrinsic_X__.Controlled.Adjoint.Apply((c, q1));
            #line hidden
            return QVoid.Instance;
        };"""

        |> testOne oneQubitOperation

        Some """
        public override Func<(IQArray<Qubit>,(Qubit,Qubit,Microsoft.Quantum.Intrinsic.Qubits)), QVoid> __ControlledAdjointBody__ => (__in__) =>
        {
            var (c,(q1,q2,arr1)) = __in__;

            __Microsoft_Quantum_Testing_three__op1__.Controlled.Adjoint.Apply((c, (q1, q2)));
            __Microsoft_Quantum_Testing_three__op1__.Controlled.Adjoint.Apply((c, (q2, q1)));
            __Microsoft_Quantum_Testing_three__op1__.Controlled.Adjoint.Apply((c, (q1, q2)));

            #line hidden
            return QVoid.Instance;
        };"""
        |> testOne threeQubitOperation

    [<Fact>]
    let ``partial application`` () =
        [
            //todo: "partial1Args.Partial<Int64>(_).Apply(1L);"
            "__Microsoft_Quantum_Testing_partial3Args__
                .Partial(new Func<(Int64,Double,Result), (Int64,Double,Result)>((__arg1__) => (__arg1__.Item1, __arg1__.Item2, __arg1__.Item3)))
                .Apply((1L, 3.5D, Result.One));"
            "__Microsoft_Quantum_Testing_partial3Args__
                .Partial(new Func<Double, (Int64,Double,Result)>((__arg2__) => (1L, __arg2__, Result.Zero)))
                .Apply(3.5D);"
            "__Microsoft_Quantum_Testing_partial3Args__
                .Partial(new Func<(Int64,Result), (Int64,Double,Result)>((__arg3__) => (__arg3__.Item1, 3.5D, __arg3__.Item2)))
                .Apply((1L, Result.Zero));"
            "__Microsoft_Quantum_Testing_partial3Args__
                .Partial(new Func<Result, (Int64,Double,Result)>((__arg4__) => (1L, 3.5D, __arg4__)))
                .Apply(Result.Zero);"
            "__Microsoft_Quantum_Testing_partial3Args__
                .Partial(new Func<(Double,Result), (Int64,Double,Result)>((__arg5__) => (1L, __arg5__.Item1, __arg5__.Item2)))
                .Apply((3.5D, Result.Zero));"
            "__Microsoft_Quantum_Testing_partialInnerTuple__
                .Partial(new Func<(Int64,(Double,Result)), (Int64,(Double,Result))>((__arg6__) => (__arg6__.Item1, (__arg6__.Item2.Item1, __arg6__.Item2.Item2))))
                .Apply((1L, (3.5D, Result.One)));"
            "__Microsoft_Quantum_Testing_partialInnerTuple__
                .Partial(new Func<(Int64,(Double,Result)), (Int64,(Double,Result))>((__arg7__) => (__arg7__.Item1, (__arg7__.Item2.Item1, __arg7__.Item2.Item2))))
                .Apply((1L, (3.5D, Result.Zero)));"
            "__Microsoft_Quantum_Testing_partialInnerTuple__
                .Partial(new Func<(Double,Result), (Int64,(Double,Result))>((__arg8__) => (1L, (__arg8__.Item1, __arg8__.Item2))))
                .Apply((3.5D, Result.Zero));"
            "__Microsoft_Quantum_Testing_partialInnerTuple__
                .Partial(new Func<(Int64,Result), (Int64,(Double,Result))>((__arg9__) => (__arg9__.Item1, (3.5D, __arg9__.Item2))))
                .Apply((1L, Result.Zero));"
            "__Microsoft_Quantum_Testing_partialInnerTuple__
                .Partial(new Func<(Int64,Double), (Int64,(Double,Result))>((__arg10__) => (__arg10__.Item1, (__arg10__.Item2, Result.One))))
                .Apply((1L, 3.5D));"
            "__Microsoft_Quantum_Testing_partialInnerTuple__
                .Partial(new Func<Result, (Int64,(Double,Result))>((__arg11__) => (1L, (3.5D, __arg11__))))
                .Apply(Result.One);"
            "__Microsoft_Quantum_Testing_partialNestedArgsOp__
                .Partial(new Func<((Int64,Int64,Int64),((Double,Double),(Result,Result,Result))), ((Int64,Int64,Int64),((Double,Double),(Result,Result,Result)))>((__arg12__) =>
                    (
                        (__arg12__.Item1.Item1, __arg12__.Item1.Item2, __arg12__.Item1.Item3),
                        (
                            (__arg12__.Item2.Item1.Item1, __arg12__.Item2.Item1.Item2),
                            (__arg12__.Item2.Item2.Item1, __arg12__.Item2.Item2.Item2, __arg12__.Item2.Item2.Item3)
                        )
                    )
                ))
                .Partial(new Func<(Int64,((Double,Double),Result)), ((Int64,Int64,Int64),((Double,Double),(Result,Result,Result)))>((__arg13__) =>
                    (
                        (1L, i, __arg13__.Item1),
                        (
                            (__arg13__.Item2.Item1.Item1, __arg13__.Item2.Item1.Item2),
                            (res, __arg13__.Item2.Item2, res)
                        )
                    )
                ))
                .Apply((1L, ((3.3D, 2D), Result.Zero)));"
            "__Microsoft_Quantum_Testing_partialNestedArgsOp__
                .Partial(new Func<(Int64,((Double,Double),Result)), ((Int64,Int64,Int64),((Double,Double),(Result,Result,Result)))>((__arg14__) =>
                    (
                        (1L, i, __arg14__.Item1),
                        (
                            (__arg14__.Item2.Item1.Item1, __arg14__.Item2.Item1.Item2),
                            (res, __arg14__.Item2.Item2, res)
                        )
                    )
                ))
                .Partial(new Func<(Double,Result), (Int64,((Double,Double),Result))>((__arg15__) =>
                    (
                        2L,
                        (
                            (2.2D, __arg15__.Item1),
                            __arg15__.Item2
                        )
                    )
                ))
                .Apply((3.3D, Result.Zero));"
            "__Microsoft_Quantum_Testing_partialNestedArgsOp__
                .Partial(new Func<(Int64,(Double,Result)), ((Int64,Int64,Int64),((Double,Double),(Result,Result,Result)))>((__arg16__) =>
                    (
                        (i, __arg16__.Item1, 1L),
                        (
                            (__arg16__.Item2.Item1, 1D), (res, __arg16__.Item2.Item2, Result.Zero)
                        )
                    )
                ))
                .Partial(new Func<Double, (Int64,(Double,Result))>((__arg17__) =>
                    (
                        i,
                        (__arg17__, res)
                    )
                ))
                .Apply(3.3D);"
            "__Microsoft_Quantum_Testing_partialGeneric1__
                .Partial(new Func<Int64, (Int64, Result, (Int64, Result))>((__arg18__) =>
                    (0L, Result.Zero, (__arg18__, Result.One))
                ))
                .Apply(1L);"
            "__Microsoft_Quantum_Testing_partialGeneric1__
                .Partial(new Func<(Int64, Result), (Int64, Result, (Int64, Result))>((__arg19__) =>
                    (__arg19__.Item1, __arg19__.Item2, (1L, Result.One))
                ))
                .Apply((0L, Result.Zero));"
            "__Microsoft_Quantum_Testing_partialGeneric1__.Partial((0L, _, (1L, _))).Apply((Result.Zero, Result.One));"
            "__Microsoft_Quantum_Testing_partialGeneric2__.Partial((0L, Result.Zero, (_, Result.One))).Apply(1L);"
            "__Microsoft_Quantum_Testing_partialGeneric2__.Partial((_, _, (1L, Result.One))).Apply((0L, Result.Zero));"
            "__Microsoft_Quantum_Testing_partialGeneric2__.Partial((0L, _, (1L, _))).Apply((Result.Zero, Result.One));"
            "partialInput
                .Partial(new Func<(Double,(Result,Result)), (Int64,(Double,Double),(Result,Result,Result))>((__arg20__) =>
                    (
                        1L,
                        (__arg20__.Item1, 1.1D),
                        (Result.Zero, __arg20__.Item2.Item1, __arg20__.Item2.Item2)
                    )
                ))
                .Apply((2.2D, (Result.One, Result.One)));"
            """
            return partialUnitary
                .Partial(new Func<IQArray<Qubit>, (Double,ICallable,IQArray<Qubit>)>((__arg21__) =>
                (
                    1.1D,
                    __Microsoft_Quantum_Testing_partialFunction__.Partial(new Func<(Int64,Double), (Int64,Double,Pauli)>((__arg22__) =>
                        (
                            __arg22__.Item1,
                            __arg22__.Item2,
                            Pauli.PauliX
                        )
                    )),
                    __arg21__)
                ));
            """
        ]
        |> testOneBody (applyVisitor partialApplicationTest)

        [
            "var r1 = __Microsoft_Quantum_Testing_partialFunction__
                .Partial(new Func<(Int64,Double,Pauli), (Int64,Double,Pauli)>((__arg1__) => (__arg1__.Item1, __arg1__.Item2, __arg1__.Item3)))
                .Apply<Result>((2L, 2.2D, Pauli.PauliY));"
            "var r2 = __Microsoft_Quantum_Testing_partialFunction__
                .Partial(new Func<(Double,Pauli), (Int64,Double,Pauli)>((__arg2__) => (1L, __arg2__.Item1, __arg2__.Item2)))
                .Partial(new Func<Pauli, (Double,Pauli)>((__arg3__) => (3.3D, __arg3__)))
                .Apply<Result>(Pauli.PauliZ);"
            "var (a,d) = t1;"
            "var (b,e) = t2;"
            "var f = new Microsoft.Quantum.Testing.F((d, e));"
            "return op.Data.Partial(new Func<IQArray<Qubit>, (Double,Microsoft.Quantum.Testing.F,IQArray<Qubit>)>((__arg4__) => (start, f, __arg4__)));"
        ]
        |> testOneBody (applyVisitor partialFunctionTest)

    [<Fact>]
    let ``buildRun test`` () =
        let testOne (_,op) expected =
            let context = createTestContext op
            let (name, nonGenericName) = findClassName context op
            let actual = buildRun context nonGenericName op.ArgumentTuple op.Signature.ArgumentType op.Signature.ReturnType |> formatSyntaxTree
            Assert.Equal(expected |> clearFormatting, actual |> clearFormatting)

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
        {
            return __m__.Run<emptyOperation, QVoid, QVoid>(QVoid.Instance);
        }"
        |> testOne emptyOperation

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Qubit q1)
        {
            return __m__.Run<oneQubitAbstractOperation, Qubit, QVoid>(q1);
        }"
        |> testOne oneQubitAbstractOperation

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Qubit q1)
        {
            return __m__.Run<oneQubitSelfAdjointAbstractOperation, Qubit, QVoid>(q1);
        }"
        |> testOne oneQubitSelfAdjointAbstractOperation


        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Qubit q1, Microsoft.Quantum.Testing.Basis b, (Pauli, IQArray<IQArray<Double>>, Boolean) t, Int64 i)
        {
            return __m__.Run<randomAbstractOperation, (Qubit, Microsoft.Quantum.Testing.Basis, (Pauli, IQArray<IQArray<Double>>, Boolean), Int64), QVoid>((q1,b,t,i));
        }"
        |> testOne randomAbstractOperation


        "public static System.Threading.Tasks.Task<IQArray<IQArray<Result>>> Run(IOperationFactory __m__, IQArray<Qubit> qubits, Microsoft.Quantum.Intrinsic.Qubits register, IQArray<IQArray<QRange>> indices, Microsoft.Quantum.Testing.arrays_T3 t)
        {
            return __m__.Run<arraysOperations, (IQArray<Qubit>, Microsoft.Quantum.Intrinsic.Qubits, IQArray<IQArray<QRange>>, Microsoft.Quantum.Testing.arrays_T3), IQArray<IQArray<Result>>>((qubits, register, indices, t));
        }"
        |> testOne arraysOperations


        "public static System.Threading.Tasks.Task<__T__> Run(IOperationFactory __m__, __T__ a1)
        {
            return __m__.Run<genC1a<__T__>, __T__, __T__>(a1);
        }"
        |> testOne genC1a


        "public static System.Threading.Tasks.Task<IQArray<__U__>> Run(IOperationFactory __m__, ICallable mapper, IQArray<__T__> source)
        {
            return __m__.Run<genMapper<__T__, __U__>, (ICallable, IQArray<__T__>), IQArray<__U__>>((mapper, source));
        }"
        |> testOne genMapper

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Int64 a, Int64 b, Double c, Double d)
        {
            return __m__.Run<nestedArgTuple1, ((Int64, Int64), (Double, Double)), QVoid>(((a,b),(c,d)));
        }"
        |> testOne nestedArgTuple1

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, (Int64, Int64) a, Double c, Int64 b, (Qubit, Qubit) d, Double e)
        {
            return __m__.Run<nestedArgTuple2, ((Int64,Int64),(Double,(Int64,(Qubit,Qubit)),Double)), QVoid>((a,(c,(b,d),e)));
        }"
        |> testOne nestedArgTuple2

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, (__A__, Int64) a, __A__ c, Int64 b, (Qubit, __A__) d, Double e)
        {
            return __m__.Run<nestedArgTupleGeneric<__A__>, ((__A__,Int64),(__A__,(Int64,(Qubit,__A__)),Double)), QVoid>((a,(c,(b,d),e)));
        }"
        |> testOne nestedArgTupleGeneric

        "public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, ICallable second, ICallable first, __B__ arg)
        {
            return __m__.Run<composeImpl<__A__,__B__>, (ICallable, ICallable, __B__), QVoid>((second, first, arg));
        }"
        |> testOne composeImpl

        "public static System.Threading.Tasks.Task<ICallable> Run(IOperationFactory __m__, ICallable second, ICallable first)
        {
            return __m__.Run<compose<__A__,__B__>, (ICallable, ICallable), ICallable>((second, first));
        }"
        |> testOne compose

    [<Fact>]
    let ``is abstract`` () =
        let testOne (_,op) expected =
            let actual = op |> isAbstract
            Assert.Equal(expected, actual)

        true  |> testOne emptyOperation
        true  |> testOne oneQubitAbstractOperation
        true  |> testOne oneQubitSelfAdjointAbstractOperation
        true  |> testOne randomAbstractOperation
        false |> testOne zeroQubitOperation
        false |> testOne oneQubitSelfAdjointOperation
        false |> testOne oneQubitOperation
        false |> testOne twoQubitOperation
        false |> testOne threeQubitOperation
        false |> testOne differentArgsOperation
        false |> testOne randomOperation

    let testOneClass (_,op : QsCallable) executionTarget (expected : string) =
        let expected = expected.Replace("%%%", HttpUtility.JavaScriptStringEncode op.SourceFile.Value)
        let assemblyConstants =
            new Collections.Generic.KeyValuePair<_,_> (AssemblyConstants.ProcessorArchitecture, executionTarget)
            |> Seq.singleton
            |> ImmutableDictionary.CreateRange
        let compilation = {Namespaces = syntaxTree; EntryPoints = ImmutableArray.Create op.FullName}
        let context = CodegenContext.Create (compilation, assemblyConstants)
        let actual = (buildOperationClass context op).ToFullString()
        Assert.Equal(expected |> clearFormatting, actual |> clearFormatting)

    [<Fact>]
    let ``buildOperationClass - concrete`` () =
        """
    public abstract partial class emptyOperation : Operation<QVoid, QVoid>, ICallable
    {
        public emptyOperation(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "emptyOperation";
        String ICallable.FullName => "Microsoft.Quantum.Testing.emptyOperation";

        public static HoneywellEntryPointInfo<QVoid, QVoid> Info => new HoneywellEntryPointInfo<QVoid, QVoid>(typeof(emptyOperation));

        public override void __Init__() { }

        public override IApplyData __DataIn__(QVoid data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
        {
            return __m__.Run<emptyOperation, QVoid, QVoid>(QVoid.Instance);
        }
    }
"""
        |> testOneClass emptyOperation AssemblyConstants.HoneywellProcessor

        """
    public abstract partial class randomAbstractOperation : Unitary<(Qubit,Microsoft.Quantum.Testing.Basis,(Pauli,IQArray<IQArray<Double>>,Boolean),Int64)>, ICallable
    {
        public randomAbstractOperation(IOperationFactory m) : base(m)
        {
        }

        public class In : QTuple<(Qubit,Microsoft.Quantum.Testing.Basis,(Pauli,IQArray<IQArray<Double>>,Boolean),Int64)>, IApplyData
        {
            public In((Qubit,Microsoft.Quantum.Testing.Basis,(Pauli,IQArray<IQArray<Double>>,Boolean),Int64) data) : base(data)
            {
            }

            System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    yield return Data.Item1;
                }
            }
        }

        String ICallable.Name => "randomAbstractOperation";
        String ICallable.FullName => "Microsoft.Quantum.Testing.randomAbstractOperation";

        public static IonQEntryPointInfo<(Qubit, Microsoft.Quantum.Testing.Basis, (Pauli, IQArray<IQArray<Double>>, Boolean), Int64), QVoid> Info => new IonQEntryPointInfo<(Qubit, Microsoft.Quantum.Testing.Basis, (Pauli, IQArray<IQArray<Double>>, Boolean), Int64), QVoid>(typeof(randomAbstractOperation));

        public override void __Init__() { }

        public override IApplyData __DataIn__((Qubit,Microsoft.Quantum.Testing.Basis,(Pauli,IQArray<IQArray<Double>>,Boolean),Int64) data) => new In(data);
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Qubit q1, Microsoft.Quantum.Testing.Basis b, (Pauli,IQArray<IQArray<Double>>,Boolean) t, Int64 i)
        {
            return __m__.Run<randomAbstractOperation, (Qubit,Microsoft.Quantum.Testing.Basis,(Pauli,IQArray<IQArray<Double>>,Boolean),Int64), QVoid>((q1, b, t, i));
        }
    }
"""
        |> testOneClass randomAbstractOperation AssemblyConstants.IonQProcessor

        """
    [SourceLocation("%%%", OperationFunctor.Body, 108, 113)]
    [SourceLocation("%%%", OperationFunctor.Adjoint, 113, 119)]
    [SourceLocation("%%%", OperationFunctor.Controlled, 119, 126)]
    [SourceLocation("%%%", OperationFunctor.ControlledAdjoint, 126, 132)]
    public partial class oneQubitOperation : Unitary<Qubit>, ICallable
    {
        public oneQubitOperation(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "oneQubitOperation";
        String ICallable.FullName => "Microsoft.Quantum.Testing.oneQubitOperation";

        public static QCIEntryPointInfo<Qubit, QVoid> Info => new QCIEntryPointInfo<Qubit, QVoid>(typeof(oneQubitOperation));

        protected IUnitary<Qubit> __Microsoft_Quantum_Intrinsic_X__ { get; set; }

        public override Func<Qubit, QVoid> __Body__ => (__in__) =>
        {
            var q1 = __in__;
            __Microsoft_Quantum_Intrinsic_X__.Apply(q1);
#line hidden
            return QVoid.Instance;
        }

        ;
        public override Func<Qubit, QVoid> __AdjointBody__ => (__in__) =>
        {
            var q1 = __in__;
            __Microsoft_Quantum_Intrinsic_X__.Adjoint.Apply(q1);
#line hidden
            return QVoid.Instance;
        }

        ;
        public override Func<(IQArray<Qubit>,Qubit), QVoid> __ControlledBody__ => (__in__) =>
        {
            var (c,q1) = __in__;
            __Microsoft_Quantum_Intrinsic_X__.Controlled.Apply((c, q1));
#line hidden
            return QVoid.Instance;
        }

        ;
        public override Func<(IQArray<Qubit>,Qubit), QVoid> __ControlledAdjointBody__ => (__in__) =>
        {
            var (c,q1) = __in__;
            __Microsoft_Quantum_Intrinsic_X__.Controlled.Adjoint.Apply((c, q1));
#line hidden
            return QVoid.Instance;
        }

        ;

        public override void __Init__()
        {
            this.__Microsoft_Quantum_Intrinsic_X__ = this.Factory.Get<IUnitary<Qubit>>(typeof(Microsoft.Quantum.Intrinsic.X));
        }

        public override IApplyData __DataIn__(Qubit data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Qubit q1)
        {
            return __m__.Run<oneQubitOperation, Qubit, QVoid>(q1);
        }
    }
"""
        |> testOneClass oneQubitOperation AssemblyConstants.QCIProcessor

    [<Fact>]
    let ``buildOperationClass - generics`` () =
        """
    public abstract partial class genCtrl3<__X__, __Y__, __Z__> : Controllable<(__X__,(Int64,(__Y__,__Z__),Result))>, ICallable
    {
        public genCtrl3(IOperationFactory m) : base(m)
        {
        }

        public class In : QTuple<(__X__,(Int64,(__Y__,__Z__),Result))>, IApplyData
        {
            public In((__X__,(Int64,(__Y__,__Z__),Result)) data) : base(data)
            {
            }

            System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    var __temp1__ = Data.Item1;
                    var __temp2__ = Data.Item2.Item2.Item1;
                    var __temp3__ = Data.Item2.Item2.Item2;
                    return Qubit.Concat(__temp1__?.GetQubits(), __temp2__?.GetQubits(), __temp3__?.GetQubits());
                }
            }
        }

        String ICallable.Name => "genCtrl3";
        String ICallable.FullName => "Microsoft.Quantum.Compiler.Generics.genCtrl3";

        public static HoneywellEntryPointInfo<(__X__, (Int64, (__Y__, __Z__), Result)), QVoid> Info => new HoneywellEntryPointInfo<(__X__, (Int64, (__Y__, __Z__), Result)), QVoid>(typeof(genCtrl3<__X__,__Y__,__Z__>));

        public override void __Init__() { }

        public override IApplyData __DataIn__((__X__,(Int64,(__Y__,__Z__),Result)) data) => new In(data);
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, __X__ arg1, (Int64,(__Y__,__Z__),Result) arg2)
        {
            return __m__.Run<genCtrl3<__X__,__Y__,__Z__>, (__X__,(Int64,(__Y__,__Z__),Result)), QVoid>((arg1, arg2));
        }
    }
"""
        |> testOneClass genCtrl3 AssemblyConstants.HoneywellProcessor

        """
    [SourceLocation("%%%", OperationFunctor.Body, 1271, 1277)]
    public partial class composeImpl<__A__, __B__> : Operation<(ICallable,ICallable,__B__), QVoid>, ICallable
    {
        public composeImpl(IOperationFactory m) : base(m)
        {
        }

        public class In : QTuple<(ICallable,ICallable,__B__)>, IApplyData
        {
            public In((ICallable,ICallable,__B__) data) : base(data)
            {
            }

            System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
            {
                get
                {
                    var __temp1__ = Data.Item3;
                    return Qubit.Concat(((IApplyData)Data.Item1)?.Qubits, ((IApplyData)Data.Item2)?.Qubits, __temp1__?.GetQubits());
                }
            }
        }

        String ICallable.Name => "composeImpl";
        String ICallable.FullName => "Microsoft.Quantum.Compiler.Generics.composeImpl";

        public static IonQEntryPointInfo<(ICallable, ICallable, __B__), QVoid> Info => new IonQEntryPointInfo<(ICallable, ICallable, __B__), QVoid>(typeof(composeImpl<__A__,__B__>));

        public override Func<(ICallable,ICallable,__B__), QVoid> __Body__ => (__in__) =>
        {
            var (second,first,arg) = __in__;
            second.Apply(first.Apply<__A__>(arg));
#line hidden
            return QVoid.Instance;
        };

        public override void __Init__() { }

        public override IApplyData __DataIn__((ICallable,ICallable,__B__) data) => new In(data);
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, ICallable second, ICallable first, __B__ arg)
        {
            return __m__.Run<composeImpl<__A__,__B__>, (ICallable,ICallable,__B__), QVoid>((second, first, arg));
        }
    }
"""
        |> testOneClass composeImpl AssemblyConstants.IonQProcessor

    [<Fact>]
    let ``buildOperationClass - abstract function`` () =
        """
    public abstract partial class genF1<__A__> : Function<__A__, QVoid>, ICallable
    {
        public genF1(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "genF1";
        String ICallable.FullName => "Microsoft.Quantum.Compiler.Generics.genF1";

        public static QCIEntryPointInfo<__A__, QVoid> Info => new QCIEntryPointInfo<__A__, QVoid>(typeof(genF1<__A__>));

        public override void __Init__() { }

        public override IApplyData __DataIn__(__A__ data) => new QTuple<__A__>(data);
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, __A__ arg)
        {
            return __m__.Run<genF1<__A__>, __A__, QVoid>(arg);
        }
    }
"""
        |> testOneClass genF1 AssemblyConstants.QCIProcessor

    [<Fact>]
    let ``buildOperationClass - access modifiers`` () =
        """
[SourceLocation("%%%", OperationFunctor.Body, 1319, 1321)]
internal partial class EmptyInternalFunction : Function<QVoid, QVoid>, ICallable
{
    public EmptyInternalFunction(IOperationFactory m) : base(m)
    {
    }

        String ICallable.Name => "EmptyInternalFunction";
        String ICallable.FullName => "Microsoft.Quantum.Compiler.Generics.EmptyInternalFunction";
        public static EntryPointInfo<QVoid, QVoid> Info => new EntryPointInfo<QVoid, QVoid>(typeof(EmptyInternalFunction));

        public override Func<QVoid, QVoid> __Body__ => (__in__) =>
        {
#line hidden
            return QVoid.Instance;
        };

        public override void __Init__()
        {
        }

        public override IApplyData __DataIn__(QVoid data) => data;

        public override IApplyData __DataOut__(QVoid data) => data;

    public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
    {
        return __m__.Run<EmptyInternalFunction, QVoid, QVoid>(QVoid.Instance);
    }
}
"""
        |> testOneClass emptyInternalFunction null

        """
[SourceLocation("%%%", OperationFunctor.Body, 1321, 1323)]
internal partial class EmptyInternalOperation : Operation<QVoid, QVoid>, ICallable
{
    public EmptyInternalOperation(IOperationFactory m) : base(m)
    {
    }

        String ICallable.Name => "EmptyInternalOperation";
        String ICallable.FullName => "Microsoft.Quantum.Compiler.Generics.EmptyInternalOperation";
        public static EntryPointInfo<QVoid, QVoid> Info => new EntryPointInfo<QVoid, QVoid>(typeof(EmptyInternalOperation));

        public override Func<QVoid, QVoid> __Body__ => (__in__) =>
        {
    #line hidden
            return QVoid.Instance;
        };

        public override void __Init__()
        {
        }

        public override IApplyData __DataIn__(QVoid data) => data;

        public override IApplyData __DataOut__(QVoid data) => data;

    public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
    {
        return __m__.Run<EmptyInternalOperation, QVoid, QVoid>(QVoid.Instance);
    }
}
"""
        |> testOneClass emptyInternalOperation null


    [<Fact>]
    let ``duplicatedDefinitionsCaller body`` () =
        [
            "__Microsoft_Quantum_Testing_emptyFunction__.Apply(QVoid.Instance);"
            "__Microsoft_Quantum_Overrides_emptyFunction__.Apply(QVoid.Instance);"
            """
            {
                var qubits = __Microsoft_Quantum_Intrinsic_Allocate__.Apply(1L);
#line hidden
                bool __arg1__ = true;
                try
                {
                    __Microsoft_Quantum_Testing_H__.Apply(qubits[0L]);
                    __Microsoft_Quantum_Intrinsic_H__.Apply(qubits[0L]);
                }
#line hidden
                catch
                {
                    __arg1__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg1__)
                    {
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(qubits);
                    }
                }
            }"""
        ]
        |> testOneBody (applyVisitor duplicatedDefinitionsCaller)


    [<Fact>]
    let ``buildOpsProperties with duplicatedDefinitionsCaller`` () =
        let t = sprintf @"protected %s __%s__ { get; set; }"
        let template (sign: string) (name: string) = t sign name

        let expected =
            [
                template "Allocate" "Microsoft_Quantum_Intrinsic_Allocate"
                template "IUnitary<Qubit>" "Microsoft_Quantum_Intrinsic_H"
                template "ICallable<Qubit, QVoid>" "Microsoft_Quantum_Testing_H"
                template "Release" "Microsoft_Quantum_Intrinsic_Release"
                template "ICallable<QVoid, QVoid>" "Microsoft_Quantum_Overrides_emptyFunction"
                template "ICallable<QVoid, QVoid>" "Microsoft_Quantum_Testing_emptyFunction"
            ]

        let (_,op) = duplicatedDefinitionsCaller
        let context = createTestContext op
        let actual =
            op
            |> operationDependencies
            |> depsByName
            |> buildOpsProperties context
            |> List.map formatSyntaxTree

        List.zip (expected |> List.map clearFormatting) (actual  |> List.map clearFormatting) |> List.iter Assert.Equal

    [<Fact>]
    let ``buildOpsProperties - internal callables`` () =
        let property = sprintf "private protected %s __%s__ { get; set; }"
        let expected =
            [
                property "ICallable<QVoid, QVoid>" "Microsoft_Quantum_Compiler_Generics_EmptyInternalFunction"
                property "ICallable<QVoid, QVoid>" "Microsoft_Quantum_Compiler_Generics_EmptyInternalOperation"
                property "ICallable<QVoid, Microsoft.Quantum.Compiler.Generics.InternalType>" "Microsoft_Quantum_Compiler_Generics_InternalType"
                property "ICallable<QVoid, Microsoft.Quantum.Compiler.Generics.InternalType>" "Microsoft_Quantum_Compiler_Generics_MakeInternalType"
            ]
        let op = snd useInternalCallables
        let actual =
            op
            |> operationDependencies
            |> depsByName
            |> buildOpsProperties (createTestContext op)
            |> List.map formatSyntaxTree
        List.zip (List.map clearFormatting expected) (List.map clearFormatting actual)
        |> List.iter Assert.Equal

    [<Fact>]
    let ``buildOperationClass - concrete functions`` () =
        """
    [SourceLocation("%%%", OperationFunctor.Body, 1306,1315)]
    public partial class UpdateUdtItems : Function<Microsoft.Quantum.Compiler.Generics.MyType2, Microsoft.Quantum.Compiler.Generics.MyType2>, ICallable
    {
        public UpdateUdtItems(IOperationFactorym) : base(m)
        {
        }

        String ICallable.Name => "UpdateUdtItems";
        String ICallable.FullName => "Microsoft.Quantum.Compiler.Generics.UpdateUdtItems";
        public static EntryPointInfo<Microsoft.Quantum.Compiler.Generics.MyType2, Microsoft.Quantum.Compiler.Generics.MyType2> Info => new EntryPointInfo<Microsoft.Quantum.Compiler.Generics.MyType2, Microsoft.Quantum.Compiler.Generics.MyType2>(typeof(UpdateUdtItems));

        public override Func<Microsoft.Quantum.Compiler.Generics.MyType2, Microsoft.Quantum.Compiler.Generics.MyType2> __Body__ => (__in__) =>
        {
            var udt = __in__;
            vararr=QArray<Int64>.Create(10L);
            return new Microsoft.Quantum.Compiler.Generics.MyType2((1L,udt.Data.Item2,(arr?.Copy(),udt.Data.Item3.Item2)));
        };

        public override void __Init__() { }

        public override IApplyData __DataIn__(Microsoft.Quantum.Compiler.Generics.MyType2data) => data;
        public override IApplyData __DataOut__(Microsoft.Quantum.Compiler.Generics.MyType2data) => data;
        public static System.Threading.Tasks.Task<Microsoft.Quantum.Compiler.Generics.MyType2> Run(IOperationFactory __m__, Microsoft.Quantum.Compiler.Generics.MyType2 udt)
        {
            return __m__.Run<UpdateUdtItems,Microsoft.Quantum.Compiler.Generics.MyType2,Microsoft.Quantum.Compiler.Generics.MyType2>(udt);
        }
    }
        """
        |> testOneClass UpdateUdtItems null


        """
    public abstract partial class emptyFunction : Function<QVoid, QVoid>, ICallable
    {
        public emptyFunction(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "emptyFunction";
        String ICallable.FullName => "Microsoft.Quantum.Overrides.emptyFunction";
        public static EntryPointInfo<QVoid, QVoid> Info => new EntryPointInfo<QVoid, QVoid>(typeof(emptyFunction));

        public override void __Init__() { }

        public override IApplyData __DataIn__(QVoid data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
        {
            return __m__.Run<emptyFunction, QVoid, QVoid>(QVoid.Instance);
        }
    }
"""
        |> testOneClass emptyFunction null

        """
    [SourceLocation("%%%", OperationFunctor.Body, 33, 40)]
    public partial class intFunction : Function<QVoid, Int64>, ICallable
    {
        public intFunction(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "intFunction";
        String ICallable.FullName => "Microsoft.Quantum.Testing.intFunction";
        public static EntryPointInfo<QVoid, Int64> Info => new EntryPointInfo<QVoid, Int64>(typeof(intFunction));

        public override Func<QVoid, Int64> __Body__ => (__in__) =>
        {
            return 1L;
        };

        public override void __Init__() { }

        public override IApplyData __DataIn__(QVoid data) => data;
        public override IApplyData __DataOut__(Int64 data) => new QTuple<Int64>(data);
        public static System.Threading.Tasks.Task<Int64> Run(IOperationFactory __m__)
        {
            return __m__.Run<intFunction, QVoid, Int64>(QVoid.Instance);
        }
    }
"""
        |> testOneClass intFunction null

        """
    [SourceLocation("%%%", OperationFunctor.Body, 45, 51)]
    public partial class powFunction : Function<(Int64,Int64), Int64>, ICallable
    {
        public powFunction(IOperationFactory m) : base(m)
        {
        }

        public class In : QTuple<(Int64,Int64)>, IApplyData
        {
            public In((Int64,Int64) data) : base(data)
            {
            }

            System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        }

        String ICallable.Name => "powFunction";
        String ICallable.FullName => "Microsoft.Quantum.Testing.powFunction";
        public static EntryPointInfo<(Int64, Int64), Int64> Info => new EntryPointInfo<(Int64, Int64), Int64>(typeof(powFunction));

        public override Func<(Int64,Int64), Int64> __Body__ => (__in__) =>
        {
            var (x,y) = __in__;
            return x.Pow(y);
        };

        public override void __Init__() { }

        public override IApplyData __DataIn__((Int64,Int64) data) => new In(data);
        public override IApplyData __DataOut__(Int64 data) => new QTuple<Int64>(data);
        public static System.Threading.Tasks.Task<Int64> Run(IOperationFactory __m__, Int64 x, Int64 y)
        {
            return __m__.Run<powFunction, (Int64,Int64), Int64>((x, y));
        }
    }
"""
        |> testOneClass powFunction null

        """
    [SourceLocation("%%%", OperationFunctor.Body, 51, 57)]
    public partial class bigPowFunction : Function<(System.Numerics.BigInteger,Int64), System.Numerics.BigInteger>, ICallable
    {
        public bigPowFunction(IOperationFactory m) : base(m)
        {
        }

        public class In : QTuple<(System.Numerics.BigInteger,Int64)>, IApplyData
        {
            public In((System.Numerics.BigInteger,Int64) data) : base(data)
            {
            }

            System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        }

        String ICallable.Name => "bigPowFunction";
        String ICallable.FullName => "Microsoft.Quantum.Testing.bigPowFunction";
        public static EntryPointInfo<(System.Numerics.BigInteger, Int64), System.Numerics.BigInteger> Info => new EntryPointInfo<(System.Numerics.BigInteger, Int64), System.Numerics.BigInteger>(typeof(bigPowFunction));

        public override Func<(System.Numerics.BigInteger,Int64), System.Numerics.BigInteger> __Body__ => (__in__) =>
        {
            var (x,y) = __in__;
            return x.Pow(y);
        };

        public override void __Init__() { }

        public override IApplyData __DataIn__((System.Numerics.BigInteger,Int64) data) => new In(data);
        public override IApplyData __DataOut__(System.Numerics.BigInteger data) => new QTuple<System.Numerics.BigInteger>(data);
        public static System.Threading.Tasks.Task<System.Numerics.BigInteger> Run(IOperationFactory __m__, System.Numerics.BigInteger x, Int64 y)
        {
            return __m__.Run<bigPowFunction, (System.Numerics.BigInteger,Int64), System.Numerics.BigInteger>((x, y));
        }
    }
"""
        |> testOneClass bigPowFunction null


    let private testOneUdt (_,udt) expected =
        let context = CodegenContext.Create syntaxTree
        let actual  = (buildUdtClass context udt).ToFullString()
        Assert.Equal(expected |> clearFormatting, actual |> clearFormatting)

    [<Fact>]
    let ``buildUdtClass - udts`` () =

        """
    public class U : UDTBase<IUnitary>, IApplyData
    {
        public U() : base(default(IUnitary))
        {
        }

        public U(IUnitary data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
        {
            get
            {
                return ((IApplyData)Data)?.Qubits;
            }
        }

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_U

        """
    public class AA : UDTBase<Microsoft.Quantum.Testing.A>, IApplyData
    {
        public AA() : base(default(Microsoft.Quantum.Testing.A))
        {
        }

        public AA(Microsoft.Quantum.Testing.A data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
        {
            get
            {
                return ((IApplyData)Data?.Data)?.Qubits;
            }
        }

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_AA

        """
    public class Q : UDTBase<Qubit>, IApplyData
    {
        public Q() : base(default(Qubit))
        {
        }

        public Q(Qubit data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
        {
            get
            {
                yield return Data;
            }
        }

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_Q

        """
    public class QQ : UDTBase<Microsoft.Quantum.Testing.Q>, IApplyData
    {
        public QQ() : base(default(Microsoft.Quantum.Testing.Q))
        {
        }

        public QQ(Microsoft.Quantum.Testing.Q data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
        {
            get
            {
                yield return Data?.Data;
            }
        }

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_QQ

        """
    public class Qubits : UDTBase<IQArray<Qubit>>, IApplyData
    {
        public Qubits() : base(new QArray<Qubit>())
        {
        }

        public Qubits(IQArray<Qubit> data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
        {
            get
            {
                return ((IApplyData)Data)?.Qubits;
            }
        }

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_Qubits

        """
    public class udt_args1 : UDTBase<(Int64,IQArray<Qubit>)>, IApplyData
    {
        public udt_args1() : base(default((Int64,IQArray<Qubit>)))
        {
        }

        public udt_args1((Int64,IQArray<Qubit>) data) : base(data)
        {
        }

        public Int64 Item1 => Data.Item1;
        public IQArray<Qubit> Item2 => Data.Item2;
        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits
        {
            get
            {
                return ((IApplyData)Data.Item2)?.Qubits;
            }
        }

        public void Deconstruct(out Int64 item1, out IQArray<Qubit> item2)
        {
            item1 = Data.Item1;
            item2 = Data.Item2;
        }
    }
"""
        |> testOneUdt udt_args1

        """
    public class udt_Real : UDTBase<Double>, IApplyData
    {
        public udt_Real() : base(default(Double))
        {
        }

        public udt_Real(Double data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_Real

        """
    public class udt_Complex : UDTBase<(Microsoft.Quantum.Testing.udt_Real,Microsoft.Quantum.Testing.udt_Real)>, IApplyData
    {
        public udt_Complex() : base(default((Microsoft.Quantum.Testing.udt_Real,Microsoft.Quantum.Testing.udt_Real)))
        {
        }

        public udt_Complex((Microsoft.Quantum.Testing.udt_Real,Microsoft.Quantum.Testing.udt_Real) data) : base(data)
        {
        }

        public Microsoft.Quantum.Testing.udt_Real Item1 => Data.Item1;
        public Microsoft.Quantum.Testing.udt_Real Item2 => Data.Item2;
        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct(out Microsoft.Quantum.Testing.udt_Real item1, out Microsoft.Quantum.Testing.udt_Real item2)
        {
            item1 = Data.Item1;
            item2 = Data.Item2;
        }
    }
"""
        |> testOneUdt udt_Complex

        """
    public class udt_TwoDimArray : UDTBase<IQArray<IQArray<Result>>>, IApplyData
    {
        public udt_TwoDimArray() : base(new QArray<IQArray<Result>>())
        {
        }

        public udt_TwoDimArray(IQArray<IQArray<Result>> data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;

        public void Deconstruct()
        {
        }
    }
"""
        |> testOneUdt udt_TwoDimArray

    [<Fact>]
    let ``buildUdtClass - access modifiers`` () =
        """
internal class InternalType : UDTBase<QVoid>, IApplyData
{
    public InternalType() : base(default(QVoid))
    {
    }

    public InternalType(QVoid data) : base(data)
    {
    }

    System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;

    public void Deconstruct()
    {
    }
}
"""
        |> testOneUdt udt_InternalType

    [<Fact>]
    let ``buildUdtClass - named tuple`` () =
        """
public class NamedTuple : UDTBase<((Int64,Double),Int64)>, IApplyData
{
    public NamedTuple() : base(default(((Int64,Double),Int64)))
    {
    }

    public NamedTuple(((Int64,Double),Int64) data) : base(data)
    {
    }

    public (Int64,Double) FirstItem => Data.Item1;
    public Int64 SecondItem => Data.Item2;
    public (Int64,Double) Item1 => Data.Item1;
    public Int64 Item2 => Data.Item2;
    System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
    public void Deconstruct(out (Int64,Double) item1, out Int64 item2)
    {
        item1 = Data.Item1;
        item2 = Data.Item2;
    }
}
"""
        |> testOneUdt udt_NamedTuple


    [<Fact>]
    let ``one file - EmptyElements`` () =
        """
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#pragma warning disable 436
#pragma warning disable 162
#pragma warning disable 1591

using System;
using Microsoft.Quantum.Core;
using Microsoft.Quantum.Intrinsic;
using Microsoft.Quantum.Simulation.Core;

[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"TypeConstructor\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":5,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item1__\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item2__\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Int\"},{\"Case\":\"Int\"}]]},\"ReturnType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\",\"Range\":{\"Case\":\"Null\"}}]},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":5,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"Documentation\":[]}")]
[assembly: TypeDeclaration("{\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":5,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"Type\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Int\"},{\"Case\":\"Int\"}]]},\"TypeItems\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"Int\"}]}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"Int\"}]}]}]]},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"TypeConstructor\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Unused\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":15}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item1__\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item2__\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Int\"},{\"Case\":\"Int\"}]]},\"ReturnType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Unused\",\"Range\":{\"Case\":\"Null\"}}]},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Unused\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":15}},\"Documentation\":[]}")]
[assembly: TypeDeclaration("{\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Unused\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":15}},\"Type\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Int\"},{\"Case\":\"Int\"}]]},\"TypeItems\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"Int\"}]}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"Int\"}]}]}]]},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"Function\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"emptyFunction\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":7,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":10},\"Item2\":{\"Line\":1,\"Column\":23}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"p\"]},\"Type\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\",\"Range\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":27},\"Item2\":{\"Line\":1,\"Column\":31}}]}}]},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":25},\"Item2\":{\"Line\":1,\"Column\":26}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\",\"Range\":{\"Case\":\"Null\"}}]},\"ReturnType\":{\"Case\":\"UnitType\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"emptyFunction\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":7,\"Item2\":45},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":5}},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"Operation\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"emptyOperation\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":8,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":25}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"UnitType\"},\"ReturnType\":{\"Case\":\"UnitType\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"emptyOperation\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":8,\"Item2\":41},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":5}},\"Documentation\":[]}")]


#line hidden
namespace Microsoft.Quantum
{
    public class Pair : UDTBase<(Int64,Int64)>, IApplyData
    {
        public Pair() : base(default((Int64,Int64)))
        {
        }

        public Pair((Int64,Int64) data) : base(data)
        {
        }

        public Int64 Item1 => Data.Item1;
        public Int64 Item2 => Data.Item2;
        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct(out Int64 item1, out Int64 item2)
        {
            item1 = Data.Item1;
            item2 = Data.Item2;
        }
    }

    public class Unused : UDTBase<(Int64,Int64)>, IApplyData
    {
        public Unused() : base(default((Int64,Int64)))
        {
        }

        public Unused((Int64,Int64) data) : base(data)
        {
        }

        public Int64 Item1 => Data.Item1;
        public Int64 Item2 => Data.Item2;
        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct(out Int64 item1, out Int64 item2)
        {
            item1 = Data.Item1;
            item2 = Data.Item2;
        }
    }

    public abstract partial class emptyFunction : Function<Microsoft.Quantum.Pair, QVoid>, ICallable
    {
        public emptyFunction(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "emptyFunction";
        String ICallable.FullName => "Microsoft.Quantum.emptyFunction";
        public override void __Init__() { }
        public override IApplyData __DataIn__(Microsoft.Quantum.Pair data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__, Microsoft.Quantum.Pair p)
        {
            return __m__.Run<emptyFunction, Microsoft.Quantum.Pair, QVoid>(p);
        }
    }

    public abstract partial class emptyOperation : Operation<QVoid, QVoid>, ICallable
    {
        public emptyOperation(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "emptyOperation";
        String ICallable.FullName => "Microsoft.Quantum.emptyOperation";
        public override void __Init__() { }
        public override IApplyData __DataIn__(QVoid data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
        {
            return __m__.Run<emptyOperation, QVoid, QVoid>(QVoid.Instance);
        }
    }
}"""
        |> testOneFile (Path.Combine("Circuits","EmptyElements.qs"))

    [<Fact>]
    let ``one file - UserDefinedTypes`` () =
        """
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#pragma warning disable 436
#pragma warning disable 162
#pragma warning disable 1591

using System;
using Microsoft.Quantum.Core;
using Microsoft.Quantum.Intrinsic;
using Microsoft.Quantum.Simulation.Core;

[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"TypeConstructor\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":5,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"Fst\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"Snd\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Int\"},{\"Case\":\"Int\"}]]},\"ReturnType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\",\"Range\":{\"Case\":\"Null\"}}]},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":5,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"Documentation\":[]}")]
[assembly: TypeDeclaration("{\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"Pair\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":5,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"Type\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Int\"},{\"Case\":\"Int\"}]]},\"TypeItems\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Named\",\"Fields\":[{\"VariableName\":\"Fst\",\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":17},\"Item2\":{\"Line\":1,\"Column\":20}}}]}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Named\",\"Fields\":[{\"VariableName\":\"Snd\",\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":28},\"Item2\":{\"Line\":1,\"Column\":31}}}]}]}]]},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"TypeConstructor\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"NestedPair\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":19}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item1__\"]},\"Type\":{\"Case\":\"Double\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]},{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"Fst\"]},\"Type\":{\"Case\":\"Bool\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item2__\"]},\"Type\":{\"Case\":\"String\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"Snd\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Double\"},{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Bool\"},{\"Case\":\"String\"}]]},{\"Case\":\"Int\"}]]}]]},\"ReturnType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"NestedPair\",\"Range\":{\"Case\":\"Null\"}}]},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"NestedPair\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":19}},\"Documentation\":[]}")]
[assembly: TypeDeclaration("{\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum\",\"Name\":\"NestedPair\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":19}},\"Type\":{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Double\"},{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"TupleType\",\"Fields\":[[{\"Case\":\"Bool\"},{\"Case\":\"String\"}]]},{\"Case\":\"Int\"}]]}]]},\"TypeItems\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"Double\"}]}]},{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Named\",\"Fields\":[{\"VariableName\":\"Fst\",\"Type\":{\"Case\":\"Bool\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":33},\"Item2\":{\"Line\":1,\"Column\":36}}}]}]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"String\"}]}]}]]},{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Named\",\"Fields\":[{\"VariableName\":\"Snd\",\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":54},\"Item2\":{\"Line\":1,\"Column\":57}}}]}]}]]}]]},\"Documentation\":[]}")]

#line hidden
namespace Microsoft.Quantum
{
    public class Pair : UDTBase<(Int64,Int64)>, IApplyData
    {
        public Pair() : base(default((Int64,Int64)))
        {
        }

        public Pair((Int64,Int64) data) : base(data)
        {
        }

        public Int64 Fst => Data.Item1;
        public Int64 Snd => Data.Item2;

        public Int64 Item1 => Data.Item1;
        public Int64 Item2 => Data.Item2;

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct(out Int64 item1, out Int64 item2)
        {
            item1 = Data.Item1;
            item2 = Data.Item2;
        }
    }

    public class NestedPair : UDTBase<(Double,((Boolean,String),Int64))>, IApplyData
    {
        public NestedPair() : base(default((Double,((Boolean,String),Int64))))
        {
        }

        public NestedPair((Double,((Boolean,String),Int64)) data) : base(data)
        {
        }

        public Boolean Fst => Data.Item2.Item1.Item1;
        public Int64 Snd => Data.Item2.Item2;

        public Double Item1 => Data.Item1;
        public ((Boolean, String), Int64) Item2 => Data.Item2;

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct(out Double item1, out ((Boolean, String), Int64) item2)
        {
            item1 = Data.Item1;
            item2 = Data.Item2;
        }
    }
}
        """
        |> testOneFile (Path.Combine("Circuits","Types.qs"))

    [<Fact>]
    let ``find local elements `` () =
        let oneName = function | QsCustomType udt -> udt.FullName.Name.Value | QsCallable  op -> op.FullName.Name.Value
        let expected = [ "H"; "M"; "Qubits"; "Qubits"; "R"; "S"; "X"; "Z"; ]     // Qubits is two times: one for UDT and one for constructor.
        let local    = syntaxTree |> findLocalElements Some (Path.GetFullPath (Path.Combine("Circuits","Intrinsic.qs")) |> NonNullable<string>.New)
        Assert.Equal(1, local.Length)
        Assert.Equal("Microsoft.Quantum.Intrinsic", (fst local.[0]).Value)
        let actual   = (snd local.[0]) |> List.map oneName |> List.sort
        List.zip expected actual |> List.iter Assert.Equal

    [<Fact>]
    let ``one file - HelloWorld`` () =
        """
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#pragma warning disable 436
#pragma warning disable 162
#pragma warning disable 1591

using System;
using Microsoft.Quantum.Core;
using Microsoft.Quantum.Intrinsic;
using Microsoft.Quantum.Simulation.Core;

[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"Operation\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Tests.Inline\",\"Name\":\"HelloWorld\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":21}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"n\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":23},\"Item2\":{\"Line\":1,\"Column\":24}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"Int\"},\"ReturnType\":{\"Case\":\"Int\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum.Tests.Inline\",\"Name\":\"HelloWorld\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":21}},\"Documentation\":[]}")]


#line hidden
namespace Microsoft.Quantum.Tests.Inline
{
    [SourceLocation("%%", OperationFunctor.Body, 7, -1)]
    public partial class HelloWorld : Operation<Int64, Int64>, ICallable
    {
        public HelloWorld(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "HelloWorld";
        String ICallable.FullName => "Microsoft.Quantum.Tests.Inline.HelloWorld";
        public override Func<Int64, Int64> __Body__ => (__in__) =>
        {
            var n = __in__;
#line 9 "%%"
            var r = (n + 1L);
#line 11 "%%"
            return r;
        };

        public override void __Init__() { }

        public override IApplyData __DataIn__(Int64 data) => new QTuple<Int64>(data);
        public override IApplyData __DataOut__(Int64 data) => new QTuple<Int64>(data);
        public static System.Threading.Tasks.Task<Int64> Run(IOperationFactory __m__, Int64 n)
        {
            return __m__.Run<HelloWorld, Int64, Int64>(n);
        }
    }
}"""
        |>
        testOneFile (Path.Combine("Circuits","HelloWorld.qs"))


    [<Fact>]
    let ``one file - LineNumbers`` () =
        """
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#pragma warning disable 436
#pragma warning disable 162
#pragma warning disable 1591

using System;
using Microsoft.Quantum.Core;
using Microsoft.Quantum.Intrinsic;
using Microsoft.Quantum.Simulation.Core;

[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"Operation\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Tests.LineNumbers\",\"Name\":\"TestLineInBlocks\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":8,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":27}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"n\"]},\"Type\":{\"Case\":\"Int\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":29},\"Item2\":{\"Line\":1,\"Column\":30}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"Int\"},\"ReturnType\":{\"Case\":\"Result\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum.Tests.LineNumbers\",\"Name\":\"TestLineInBlocks\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":8,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":27}},\"Documentation\":[]}")]
#line hidden
namespace Microsoft.Quantum.Tests.LineNumbers
{
    [SourceLocation("%%", OperationFunctor.Body, 9, -1)]
    public partial class TestLineInBlocks : Operation<Int64, Result>, ICallable
    {
        public TestLineInBlocks(IOperationFactory m) : base(m)
        {
        }

        String ICallable.Name => "TestLineInBlocks";
        String ICallable.FullName => "Microsoft.Quantum.Tests.LineNumbers.TestLineInBlocks";
        protected Allocate __Microsoft_Quantum_Intrinsic_Allocate__
        {
            get;
            set;
        }

        protected Release __Microsoft_Quantum_Intrinsic_Release__
        {
            get;
            set;
        }

        protected IUnitary<Qubit> __Microsoft_Quantum_Intrinsic_X__
        {
            get;
            set;
        }

        public override Func<Int64, Result> __Body__ => (__in__) =>
        {
            var n = __in__;
#line 11 "%%"
            var r = (n + 1L);
#line hidden
            {
#line 13 "%%"
                var (ctrls,q) = (__Microsoft_Quantum_Intrinsic_Allocate__.Apply(r), __Microsoft_Quantum_Intrinsic_Allocate__.Apply());
#line hidden
                bool __arg1__ = true;
                try
                {
#line 15 "%%"
                    if ((n == 0L))
                    {
#line 16 "%%"
                        __Microsoft_Quantum_Intrinsic_X__.Apply(q);
                    }
                    else
                    {
#line 20 "%%"
                        foreach (var c in ctrls?.Slice(new QRange(0L, 2L, r)))
#line hidden
                        {
#line 21 "%%"
                            __Microsoft_Quantum_Intrinsic_X__.Controlled.Apply((new QArray<Qubit>(c), q));
                        }
                    }
                }
#line hidden
                catch
                {
                    __arg1__ = false;
                    throw;
                }
#line hidden
                finally
                {
                    if (__arg1__)
                    {
#line hidden
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(ctrls);
#line hidden
                        __Microsoft_Quantum_Intrinsic_Release__.Apply(q);
                    }
                }
            }

#line 26 "%%"
            return Result.Zero;
        }

        ;
        public override void __Init__()
        {
            this.__Microsoft_Quantum_Intrinsic_Allocate__ = this.Factory.Get<Allocate>(typeof(Microsoft.Quantum.Intrinsic.Allocate));
            this.__Microsoft_Quantum_Intrinsic_Release__ = this.Factory.Get<Release>(typeof(Microsoft.Quantum.Intrinsic.Release));
            this.__Microsoft_Quantum_Intrinsic_X__ = this.Factory.Get<IUnitary<Qubit>>(typeof(Microsoft.Quantum.Intrinsic.X));
        }

        public override IApplyData __DataIn__(Int64 data) => new QTuple<Int64>(data);
        public override IApplyData __DataOut__(Result data) => new QTuple<Result>(data);
        public static System.Threading.Tasks.Task<Result> Run(IOperationFactory __m__, Int64 n)
        {
            return __m__.Run<TestLineInBlocks, Int64, Result>(n);
        }
    }
}"""
        |>
        testOneFile (Path.Combine("Circuits","LineNumbers.qs"))


    [<Fact>]
    let ``one file - UnitTests`` () =
        """
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#pragma warning disable 436
#pragma warning disable 162
#pragma warning disable 1591

using System;
using Microsoft.Quantum.Core;
using Microsoft.Quantum.Intrinsic;
using Microsoft.Quantum.Simulation.Core;

[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"TypeConstructor\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Core\",\"Name\":\"Attribute\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":18}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item1__\"]},\"Type\":{\"Case\":\"UnitType\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"UnitType\"},\"ReturnType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Core\",\"Name\":\"Attribute\",\"Range\":{\"Case\":\"Null\"}}]},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum.Core\",\"Name\":\"Attribute\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":18}},\"Documentation\":[]}")]
[assembly: TypeDeclaration("{\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Core\",\"Name\":\"Attribute\"},\"Attributes\":[{\"TypeId\":{\"Case\":\"Value\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Core\",\"Name\":\"Attribute\",\"Range\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":2},\"Item2\":{\"Line\":1,\"Column\":11}}]}}]},\"Argument\":{\"Item1\":{\"Case\":\"UnitValue\"},\"Item2\":[],\"Item3\":{\"Case\":\"UnitType\"},\"Item4\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Item5\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":13}}]}},\"Offset\":{\"Item1\":5,\"Item2\":4},\"Comments\":{\"OpeningComments\":[],\"ClosingComments\":[]}}],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":6,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":18}},\"Type\":{\"Case\":\"UnitType\"},\"TypeItems\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"UnitType\"}]}]}]]},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"TypeConstructor\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\"},\"Attributes\":[],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":12,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"VariableName\":{\"Case\":\"ValidName\",\"Fields\":[\"__Item1__\"]},\"Type\":{\"Case\":\"String\"},\"InferredInformation\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Position\":{\"Case\":\"Null\"},\"Range\":{\"Item1\":{\"Line\":1,\"Column\":1},\"Item2\":{\"Line\":1,\"Column\":1}}}]}]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"String\"},\"ReturnType\":{\"Case\":\"UserDefinedType\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\",\"Range\":{\"Case\":\"Null\"}}]},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":true}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":12,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"Documentation\":[]}")]
[assembly: TypeDeclaration("{\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\"},\"Attributes\":[{\"TypeId\":{\"Case\":\"Value\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Core\",\"Name\":\"Attribute\",\"Range\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":2},\"Item2\":{\"Line\":1,\"Column\":11}}]}}]},\"Argument\":{\"Item1\":{\"Case\":\"UnitValue\"},\"Item2\":[],\"Item3\":{\"Case\":\"UnitType\"},\"Item4\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Item5\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":13}}]}},\"Offset\":{\"Item1\":11,\"Item2\":4},\"Comments\":{\"OpeningComments\":[],\"ClosingComments\":[]}}],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":12,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":9},\"Item2\":{\"Line\":1,\"Column\":13}},\"Type\":{\"Case\":\"String\"},\"TypeItems\":{\"Case\":\"QsTuple\",\"Fields\":[[{\"Case\":\"QsTupleItem\",\"Fields\":[{\"Case\":\"Anonymous\",\"Fields\":[{\"Case\":\"String\"}]}]}]]},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"Operation\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Tests.UnitTests\",\"Name\":\"UnitTest1\"},\"Attributes\":[{\"TypeId\":{\"Case\":\"Value\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\",\"Range\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":2},\"Item2\":{\"Line\":1,\"Column\":6}}]}}]},\"Argument\":{\"Item1\":{\"Case\":\"StringLiteral\",\"Fields\":[\"ToffoliSimulator\",[]]},\"Item2\":[],\"Item3\":{\"Case\":\"String\"},\"Item4\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Item5\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":7},\"Item2\":{\"Line\":1,\"Column\":25}}]}},\"Offset\":{\"Item1\":20,\"Item2\":4},\"Comments\":{\"OpeningComments\":[],\"ClosingComments\":[]}},{\"TypeId\":{\"Case\":\"Value\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\",\"Range\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":2},\"Item2\":{\"Line\":1,\"Column\":6}}]}}]},\"Argument\":{\"Item1\":{\"Case\":\"StringLiteral\",\"Fields\":[\"QuantumSimulator\",[]]},\"Item2\":[],\"Item3\":{\"Case\":\"String\"},\"Item4\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Item5\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":7},\"Item2\":{\"Line\":1,\"Column\":25}}]}},\"Offset\":{\"Item1\":19,\"Item2\":4},\"Comments\":{\"OpeningComments\":[],\"ClosingComments\":[]}}],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":21,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":20}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"UnitType\"},\"ReturnType\":{\"Case\":\"UnitType\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum.Tests.UnitTests\",\"Name\":\"UnitTest1\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":21,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":20}},\"Documentation\":[]}")]
[assembly: CallableDeclaration("{\"Kind\":{\"Case\":\"Operation\"},\"QualifiedName\":{\"Namespace\":\"Microsoft.Quantum.Tests.UnitTests\",\"Name\":\"UnitTest2\"},\"Attributes\":[{\"TypeId\":{\"Case\":\"Value\",\"Fields\":[{\"Namespace\":\"Microsoft.Quantum.Diagnostics\",\"Name\":\"Test\",\"Range\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":2},\"Item2\":{\"Line\":1,\"Column\":6}}]}}]},\"Argument\":{\"Item1\":{\"Case\":\"StringLiteral\",\"Fields\":[\"SomeNamespace.CustomSimulator\",[]]},\"Item2\":[],\"Item3\":{\"Case\":\"String\"},\"Item4\":{\"IsMutable\":false,\"HasLocalQuantumDependency\":false},\"Item5\":{\"Case\":\"Value\",\"Fields\":[{\"Item1\":{\"Line\":1,\"Column\":7},\"Item2\":{\"Line\":1,\"Column\":38}}]}},\"Offset\":{\"Item1\":24,\"Item2\":4},\"Comments\":{\"OpeningComments\":[],\"ClosingComments\":[]}}],\"Modifiers\":{\"Access\":{\"Case\":\"DefaultAccess\"}},\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":25,\"Item2\":4},\"SymbolRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":20}},\"ArgumentTuple\":{\"Case\":\"QsTuple\",\"Fields\":[[]]},\"Signature\":{\"TypeParameters\":[],\"ArgumentType\":{\"Case\":\"UnitType\"},\"ReturnType\":{\"Case\":\"UnitType\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}}},\"Documentation\":[]}")]
[assembly: SpecializationDeclaration("{\"Kind\":{\"Case\":\"QsBody\"},\"TypeArguments\":{\"Case\":\"Null\"},\"Information\":{\"Characteristics\":{\"Case\":\"EmptySet\"},\"InferredInformation\":{\"IsSelfAdjoint\":false,\"IsIntrinsic\":false}},\"Parent\":{\"Namespace\":\"Microsoft.Quantum.Tests.UnitTests\",\"Name\":\"UnitTest2\"},\"Attributes\":[],\"SourceFile\":\"%%%\",\"Position\":{\"Item1\":25,\"Item2\":4},\"HeaderRange\":{\"Item1\":{\"Line\":1,\"Column\":11},\"Item2\":{\"Line\":1,\"Column\":20}},\"Documentation\":[]}")]

#line hidden
namespace Microsoft.Quantum.Core
{
    public class Attribute : UDTBase<QVoid>, IApplyData
    {
        public Attribute() : base(default(QVoid))
        {
        }

        public Attribute(QVoid data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct()
        {
        }
    }
}

#line hidden
namespace Microsoft.Quantum.Diagnostics
{
    public class Test : UDTBase<String>, IApplyData
    {
        public Test() : base(default(String))
        {
        }

        public Test(String data) : base(data)
        {
        }

        System.Collections.Generic.IEnumerable<Qubit> IApplyData.Qubits => null;
        public void Deconstruct()
        {
        }
    }
}

#line hidden
namespace Microsoft.Quantum.Tests.UnitTests
{
    [SourceLocation("%%", OperationFunctor.Body, 22, 26)]
    public partial class UnitTest1 : Operation<QVoid, QVoid>, ICallable
    {
        public UnitTest1(IOperationFactory m) : base(m)
        {
        }

        public class QuantumSimulator
        {
            public QuantumSimulator(Xunit.Abstractions.ITestOutputHelper Output)
            {
                this.Output = Output;
            }

            internal Xunit.Abstractions.ITestOutputHelper Output
            {
                get;
            }

            [Xunit.Fact()]
            [Xunit.Trait("Target", "QuantumSimulator")]
            [Xunit.Trait("Name", "UnitTest1")]
            public void UnitTest1()
#line 22 "%%"
            {
                var sim = new Microsoft.Quantum.Simulation.Simulators.QuantumSimulator();
                if (sim is Microsoft.Quantum.Simulation.Common.SimulatorBase baseSim && this.Output != null)
                {
                    baseSim.OnLog += this.Output.WriteLine;
                }

                sim.Run<UnitTest1, QVoid, QVoid>(QVoid.Instance).Wait();
                if (sim is IDisposable disposeSim)
                {
                    disposeSim.Dispose();
                }
            }
        }

        public class ToffoliSimulator
        {
            public ToffoliSimulator(Xunit.Abstractions.ITestOutputHelper Output)
            {
                this.Output = Output;
            }

            internal Xunit.Abstractions.ITestOutputHelper Output
            {
                get;
            }

            [Xunit.Fact()]
            [Xunit.Trait("Target", "ToffoliSimulator")]
            [Xunit.Trait("Name", "UnitTest1")]
            public void UnitTest1()
#line 22 "%%"
            {
                var sim = new Microsoft.Quantum.Simulation.Simulators.ToffoliSimulator();
                if (sim is Microsoft.Quantum.Simulation.Common.SimulatorBase baseSim && this.Output != null)
                {
                    baseSim.OnLog += this.Output.WriteLine;
                }

                sim.Run<UnitTest1, QVoid, QVoid>(QVoid.Instance).Wait();
                if (sim is IDisposable disposeSim)
                {
                    disposeSim.Dispose();
                }
            }
        }

        String ICallable.Name => "UnitTest1";
        String ICallable.FullName => "Microsoft.Quantum.Tests.UnitTests.UnitTest1";

        public override Func<QVoid, QVoid> __Body__ => (__in__) =>
        {
        #line hidden
            return QVoid.Instance;
        }

        ;
        public override void __Init__()
        {
        }

        public override IApplyData __DataIn__(QVoid data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
        {
            return __m__.Run<UnitTest1, QVoid, QVoid>(QVoid.Instance);
        }
    }

    [SourceLocation("%%", OperationFunctor.Body, 26, -1)]
    public partial class UnitTest2 : Operation<QVoid, QVoid>, ICallable
    {
        public UnitTest2(IOperationFactory m) : base(m)
        {
        }

        public class CustomSimulator
        {
            public CustomSimulator(Xunit.Abstractions.ITestOutputHelper Output)
            {
                this.Output = Output;
            }

            internal Xunit.Abstractions.ITestOutputHelper Output
            {
                get;
            }

            [Xunit.Fact()]
            [Xunit.Trait("Target", "CustomSimulator")]
            [Xunit.Trait("Name", "UnitTest2")]
            public void UnitTest2()
#line 26 "%%"
            {
                var sim = new SomeNamespace.CustomSimulator();
                if (sim is Microsoft.Quantum.Simulation.Common.SimulatorBase baseSim && this.Output != null)
                {
                    baseSim.OnLog += this.Output.WriteLine;
                }

                sim.Run<UnitTest2, QVoid, QVoid>(QVoid.Instance).Wait();
                if (sim is IDisposable disposeSim)
                {
                    disposeSim.Dispose();
                }
            }
        }

        String ICallable.Name => "UnitTest2";
        String ICallable.FullName => "Microsoft.Quantum.Tests.UnitTests.UnitTest2";

        public override Func<QVoid, QVoid> __Body__ => (__in__) =>
        {
        #line hidden
            return QVoid.Instance;
        }

        ;
        public override void __Init__()
        {
        }

        public override IApplyData __DataIn__(QVoid data) => data;
        public override IApplyData __DataOut__(QVoid data) => data;
        public static System.Threading.Tasks.Task<QVoid> Run(IOperationFactory __m__)
        {
            return __m__.Run<UnitTest2, QVoid, QVoid>(QVoid.Instance);
        }
    }

}
"""
        |>
        testOneFile (Path.Combine("Circuits","UnitTests.qs"))



