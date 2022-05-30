using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace Oso.Tests;

public record class MyClass
{
    public string Name { get; set; }
    public int Id { get; set; }

    public MyClass(string name, int id)
    {
        Name = name;
        Id = id;
    }
    public string MyMethod(string arg) => arg;
    public List<string> MyList() => new() { "hello", "world" };
    public MySubClass MySubClass(string name, int id) => new MySubClass(name, id);
    public IEnumerable<string> MyEnumeration() => new List<string>() { "hello", "world" };
    public static string MyStaticMethod() => "hello world";
    public string? MyReturnNull() => null;
}

public record class MySubClass : MyClass
{
    public MySubClass(string name, int id) : base(name, id) { }
}

public class PolarTests
{
    private static readonly string TestPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..", "..", "..");
    #region Test Query
    [Fact]
    public void TestLoadAndQueryStr()
    {
        var polar = new Polar();
        polar.LoadStr("f(1);");
        Query query = polar.NewQuery("f(x)", 0);
        // TODO: Are any of these strings actually nullable? If not, we should go back and mark them as non-nullable.
        var result = query.Results.ToList()[0];
        Assert.Equal(new() { { "x", 1 } }, result);
    }

    [Fact]
    public void TestInlineQueries()
    {
        var polar = new Polar();
        polar.LoadStr("f(1); ?= f(1);");
        polar.ClearRules();
        try
        {
            var exception = Assert.Throws<OsoException>(() => polar.LoadStr("f(1); ?= f(2);"));
        }
        catch (Exception e)
        {
            throw new Exception("Expected inline query to fail but it didn't.", e);
        }
    }

    [Fact]
    public void TestBasicQueryPredicate()
    {
        // test basic query
        var polar = new Polar();
        polar.LoadStr("f(a, b) if a = b;");
        Assert.True(polar.QueryRule("f", 1, 1).Results.Any(), "Basic predicate query failed.");
        Assert.False(
            polar.QueryRule("f", 1, 2).Results.Any(),
            "Basic predicate query expected to fail but didn't.");
    }
    [Fact]
    public void TestQueryPredWithObject()
    {
        // test query with .NET Object
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        polar.LoadStr("g(x) if x.Id = 1;");
        Assert.True(
            polar.QueryRule("g", new MyClass("test", 1)).Results.Any(),
            "Predicate query with .NET object failed.");
        Assert.False(
            polar.QueryRule("g", new MyClass("test", 2)).Results.Any(),
            "Predicate query with .NET object expected to fail but didn't.");
    }

    [Fact]
    public void TestQueryPredWithVariable()
    {
        // test query with Variable
        var polar = new Polar();
        polar.LoadStr("f(a, b) if a = b;");
        var expected = new List<Dictionary<string, object>>() { new() { { "result", 1 } } };
        try
        {
            Assert.Equal(expected, polar.QueryRule("f", 1, new Variable("result")).Results);
        }
        catch (Exception e)
        {
            throw new Exception("Predicate query with Variable failed.", e);
        }
    }

    #endregion
    #region Test FFI Conversions

    [Fact]
    public void TestBoolFFIRoundTrip()
    {
        var polar = new Polar();
        bool b = true;
        JsonElement jsonTerm = polar.Host.SerializePolarTerm(b);
        object objectTerm = polar.Host.ParsePolarTerm(jsonTerm);
        Assert.Equal(b, objectTerm);
    }

    [Fact]
    public void TestIntFFIRoundTrip()
    {
        var polar = new Polar();
        int i = 3;
        JsonElement p = polar.Host.SerializePolarTerm(i);
        object o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(i, o);
    }

    [Fact]
    public void TestFloatFFIRoundTrip()
    {
        var polar = new Polar();
        double f = 3.50;
        JsonElement p = polar.Host.SerializePolarTerm(f);
        object o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(f, o);
    }

    [Fact]
    public void TestListFFIRoundTrip()
    {
        var polar = new Polar();
        List<int> l = new() { 1, 2, 3, 4 };
        JsonElement p = polar.Host.SerializePolarTerm(l);
        object o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(l, o);
    }

    [Fact]
    public void TestArrayFFIRoundTrip()
    {
        var polar = new Polar();
        int[] a1 = { 1, 2, 3, 4 };
        JsonElement p = polar.Host.SerializePolarTerm(a1);
        object o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(new List<int>() { 1, 2, 3, 4 }, o);

        double[] a2 = { 1.2, 3.5 };
        p = polar.Host.SerializePolarTerm(a2);
        o = polar.Host.ParsePolarTerm(p);

        Assert.Equal(new List<double>() { 1.2, 3.5 }, o);

        string[] a3 = { "hello", "world" };
        p = polar.Host.SerializePolarTerm(a3);
        o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(new List<string>() { "hello", "world" }, o);
    }

    [Fact]
    public void TestDictFFIRoundTrip()
    {
        var polar = new Polar();
        Dictionary<string, dynamic> m = new() { { "a", 1 }, { "b", "two" } };
        JsonElement p = polar.Host.SerializePolarTerm(m);
        object o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(m, o);
    }

    [Fact]
    public void TestDotnetClassFFIRoundTrip()
    {
        var polar = new Polar();
        MyClass instance = new MyClass("test", 1);
        JsonElement p = polar.Host.SerializePolarTerm(instance);
        object o = polar.Host.ParsePolarTerm(p);
        Assert.Equal(instance, o);
    }

    [Fact]
    public void TestPredicateFFIRoundTrip()
    {
        var polar = new Polar();
        Predicate pred = new Predicate("name", new List<object>() { 1, "hello" });
        JsonElement t = polar.Host.SerializePolarTerm(pred);
        object o = polar.Host.ParsePolarTerm(t);
        Assert.Equal(pred, o);
    }

    [Fact]
    public void TestNaN()
    {
        var polar = new Polar();
        polar.RegisterConstant(double.NaN, "nan");

        Dictionary<string, object>? result = polar.NewQuery("x = nan", 0).Results.First();
        object x = result["x"];
        Assert.True(x is double);
        double y = (double)x;
        Assert.True(double.IsNaN(y));

        Assert.True(!polar.NewQuery("nan = nan", 1).Results.Any(), "NaN != NaN");
    }

    [Fact]
    public void TestInfinities()
    {
        var polar = new Polar();
        polar.RegisterConstant(double.PositiveInfinity, "inf");

        Dictionary<string, object> infResult = polar.NewQuery("x = inf", 0).Results.First();
        object inf = infResult["x"];
        Assert.True((double)inf == double.PositiveInfinity);

        Assert.True(polar.NewQuery("inf = inf", 0).Results.Any(), "Infinity == Infinity");

        polar.RegisterConstant(double.NegativeInfinity, "neg_inf");

        Dictionary<string, object> negInfResult = polar.NewQuery("x = neg_inf", 0).Results.First();
        var negInf = (double)negInfResult["x"];
        Assert.True(negInf == double.NegativeInfinity);

        Assert.True(polar.NewQuery("neg_inf = neg_inf", 0).Results.Any(), "-Infinity == -Infinity");

        Assert.False(polar.NewQuery("inf = neg_inf", 0).Results.Any(), "Infinity != -Infinity");
        Assert.False(polar.NewQuery("inf < neg_inf", 0).Results.Any(), "Infinity > -Infinity");
        Assert.True(polar.NewQuery("neg_inf < inf", 0).Results.Any(), "-Infinity < Infinity");
    }

    [Fact]
    public void TestNil()
    {
        var polar = new Polar();
        polar.LoadStr("null(nil);");

        List<Dictionary<string, object>> expected = new() { new() { { "x", null! } } };
        Assert.Equal(polar.NewQuery("null(x)", 0).Results, expected);
        Assert.Equal(new List<Dictionary<string, object>>() { new() }, polar.QueryRule("null", args: new object?[] { null }).Results);
        Assert.False(polar.QueryRule("null", bindings: new()).Results.Any());
    }
    #endregion
    #region Test Externals

    [Fact]
    public void TestRegisterAndMakeClass()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        MyClass instance = (MyClass)polar.Host.MakeInstance("MyClass", new() { "testName", 1 }, 0UL);
        Assert.Equal("testName", instance.Name);
        Assert.Equal(1, instance.Id);
        // TODO: test that errors when given invalid constructor
        // TODO: test that errors when registering same class twice
        // TODO: test that errors if same alias used twice
        // TODO: test inheritance
    }

    [Fact]
    public void TestDuplicateRegistration()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        var exception = Assert.Throws<DuplicateClassAliasException>(() => polar.RegisterClass(typeof(MyClass), "MyClass"));
        Assert.Equal("Attempted to alias `MyClass` as `Oso.Tests.MyClass`, but `Oso.Tests.MyClass` already has that alias.", exception.Message);
    }

    [Fact]
    public void TestMakeInstanceFromPolar()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        polar.LoadStr("f(x) if x = new MyClass(\"test\", 1);");
        Query query = polar.NewQuery("f(x)", 0);
        MyClass ret = (MyClass)query.Results.First()["x"];
        Assert.Equal("test", ret.Name);
        Assert.Equal(1, ret.Id);
    }

    [Fact(Skip = "Not yet implemented")]
    public void TestKeywordArgs()
    {
        var polar = new Polar();
        // TODO: Is this supposed to be RegisterConstant?
        // polar.RegisterConstant(true, "MyClass");
        polar.RegisterClass(typeof(MyClass), "MyClass");
        var e1 = Assert.Throws<OsoException>(() => polar.NewQuery("x = new MyClass(\"test\", id: 1)", 0));
        Assert.Equal("Failed to instantiate external class MyClass; named arguments are not supported in .NET", e1.Message);
        var e2 = Assert.Throws<InvalidCallException>(() => polar.NewQuery("x = (new MyClass(\"test\", 1)).Foo(\"test\", id: 1)", 0));
        Assert.Equal("The .NET Oso library does not support keyword arguments", e2.Message);
    }

    [Fact]
    public void TestExternalCall()
    {
        // Test get attribute
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        polar.LoadStr("id(x) if x = new MyClass(\"test\", 1).Id;");
        var expected1 = new List<Dictionary<string, object>>() { new() { { "x", (object)1 } } };
        Assert.True(
            polar.NewQuery("id(x)", 0).Results.First().SequenceEqual(expected1.First()),
            "Failed to get attribute on external instance.");

        polar.ClearRules();

        // Test call method
        polar.LoadStr("method(x) if x = new MyClass(\"test\", 1).MyMethod(\"hello world\");");
        var expected2 = new Dictionary<string, object>() { { "x", "hello world" } };
        Assert.True(
            polar.NewQuery("method(x)", 0).Results.First().SequenceEqual(expected2),
            "Failed to get attribute on external instance.");
    }

    [Fact]
    public void TestReturnInstanceFromCall()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        MyClass c = new MyClass("test", 1);
        polar.LoadStr("test(c: MyClass) if x = c.MySubClass(c.Name, c.Id) and x.Id = c.Id;");
        Assert.NotEmpty(polar.QueryRule("test", c).Results);
    }

    [Fact]
    public void TestEnumerationCallResults()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        MyClass c = new MyClass("test", 1);
        polar.LoadStr("test(c: MyClass, x) if x in c.MyEnumeration();");
        var results = polar.QueryRule("test", c, new Variable("x")).Results;
        List<Dictionary<string, object>> expected = new()
        {
            new() { { "x", "hello" } },
            new() { { "x", "world" } }
        };
        Assert.Equal(expected, results, new ResultsComparer());
    }

    [Fact]
    public void TestStringMethods()
    {
        var polar = new Polar();
        // TODO: Is string.Length() defined in Polar, or in the Host?
        polar.LoadStr("f(x) if x.Length = 3;");
        Assert.NotEmpty(polar.NewQuery("f(\"oso\")", 0).Results);
        Assert.Empty(polar.NewQuery("f(\"notoso\")", 0).Results);
    }

    [Fact]
    public void TestListMethods()
    {
        var polar = new Polar();
        // TODO: is size() part of Polar, or just a reference to .NET List objects?
        // polar.Load("f(x) if x.size() = 3;");
        polar.LoadStr("f(x) if x.Count = 3;");
        Assert.True(polar.QueryRule("f", new List<int> { 1, 2, 3 }).Results.Any());
        Assert.False(polar.QueryRule("f", new List<int> { 1, 2, 3, 4 }).Results.Any());

        Assert.True(polar.QueryRule("f", new int[] { 1, 2, 3 }).Results.Any());
        Assert.False(polar.QueryRule("f", new int[] { 1, 2, 3, 4 }).Results.Any());
    }

    [Fact]
    public void TestExternalIsa()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        polar.RegisterClass(typeof(MySubClass), "MySubClass");
        polar.LoadStr("f(a: MyClass, x) if x = a.Id;");
        var expected = new List<Dictionary<string, object>>() { new() { { "x", 1 } } };
        var results = polar.QueryRule("f", new MyClass("test", 1), new Variable("x")).Results;
        Assert.Equal(expected, results, new ResultsComparer());
        polar.ClearRules();

        polar.LoadStr("f(a: MySubClass, x) if x = a.Id;");
        results = polar.QueryRule("f", new MyClass("test", 1), new Variable("x")).Results;
        Assert.False(results.Any(), "Failed to filter rules by specializers.");
        polar.ClearRules();

        polar.LoadStr("f(a: OtherClass, x) if x = a.Id;");
        var exception = Assert.Throws<UnregisteredClassException>(() => polar.QueryRule("f", new MyClass("test", 1), new Variable("x")));
        Assert.Equal("OtherClass", exception.ClassName);
    }

    [Fact]
    public void TestExternalIsSubSpecializer()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        polar.RegisterClass(typeof(MySubClass), "MySubClass");
        string policy = "f(_: MySubClass, x) if x = 1;\n" + "f(_: MyClass, x) if x = 2;";
        polar.LoadStr(policy);
        var results = polar.QueryRule("f", new MySubClass("test", 1), new Variable("x")).Results;
        var expected = new List<Dictionary<string, object>>
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } }
        };
        Assert.True(
            new ResultsComparer().Equals(results, expected),
            "Failed to order rules based on specializers.");

        results = polar.QueryRule("f", new MyClass("test", 1), new Variable("x")).Results;
        expected = new() { new() { { "x", 2 } } };
        Assert.True(new ResultsComparer().Equals(results, expected), "Failed to order rules based on specializers.");
    }

    [Fact]
    public void TestExternalUnify()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        Assert.True(polar.NewQuery("new MyClass(\"foo\", 1) = new MyClass(\"foo\", 1)", 0).Results.Any());
        Assert.False(polar.NewQuery("new MyClass(\"foo\", 1) = new MyClass(\"foo\", 2)", 0).Results.Any());
        Assert.False(polar.NewQuery("new MyClass(\"foo\", 1) = new MyClass(\"bar\", 1)", 0).Results.Any());
        Assert.False(polar.NewQuery("new MyClass(\"foo\", 1) = {foo: 1}", 0).Results.Any());
    }

    [Fact]
    public void TestExternalInternalUnify()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(System.Text.StringBuilder), "StringBuilder");
        Assert.True(polar.NewQuery("new StringBuilder(\"foo\").ToString() = \"foo\"", 0).Results.Any());
    }

    [Fact]
    public void TestReturnListFromCall()
    {
        var polar = new Polar();
        polar.LoadStr("test(c: MyClass) if \"hello\" in c.MyList();");
        polar.RegisterClass(typeof(MyClass));
        MyClass c = new MyClass("test", 1);
        Assert.True(polar.QueryRule("test", c).Results.Any());
    }

    [Fact]
    public void TestClassMethods()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass));
        polar.LoadStr("test(x) if x=1 and MyClass.MyStaticMethod() = \"hello world\";");
        Assert.True(polar.NewQuery("test(1)", 0).Results.Any());
    }

    [Fact]
    public void TestExternalOp()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(Uri), "Uri");
        Assert.True(polar.NewQuery("new Uri(\"scheme://foo\") == new Uri(\"scheme://foo\")", 0).Results.Any());
    }
    #endregion

    #region Test Parsing
    [Fact]
    public void TestIntegerOverFlowError()
    {
        var polar = new Polar();
        string rule = "f(x) if x = 18446744073709551616;";
        var e = Assert.Throws<OsoException>(() => polar.LoadStr(rule));
        Assert.StartsWith("Integer overflow: '18446744073709551616' caused an integer overflow at line 1, column 13", e.Message);
    }

    [Fact]
    public void TestInvalidTokenCharacter()
    {
        var polar = new Polar();
        string rule = "f(x) if x = \"This is not\n allowed\"";
        var e = Assert.Throws<OsoException>(() => polar.LoadStr(rule));
        // TODO: this is a wacky message
        Assert.StartsWith("Invalid token character: '\\n' is not a valid character. Found in This is not at line 1, column 25", e.Message);
    }

    [Fact]
    public void TestUnrecognizedTokenError()
    {
        var polar = new Polar();
        string rule = "1";
        var e = Assert.Throws<OsoException>(() => polar.LoadStr(rule));
        Assert.StartsWith("Unrecognized token: did not expect to find the token '1' at line 1, column 1", e.Message);
    }
    #endregion
    #region Test Loading
    [Fact]
    public void TestLoadFile()
    {
        var polar = new Polar();
        polar.LoadFiles(Path.Join(TestPath, "Resources", "test.polar"));
        List<Dictionary<string, object>> expected = new()
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } },
            new() { { "x", 3 } },
        };
        Assert.Equal(expected, polar.NewQuery("f(x)", 0).Results, new ResultsComparer());
    }

    [Fact]
    public void TestLoadNonPolarFile()
    {
        var polar = new Polar();
        try
        {
            var exception = Assert.Throws<OsoException>(() => polar.LoadFiles("wrong.txt"));
            Assert.Equal("Polar file extension missing: wrong.txt", ((AggregateException)exception.InnerException).InnerExceptions[0].Message);
        }
        catch (Xunit.Sdk.ThrowsException)
        {
            throw new XunitException("Failed to catch incorrect Polar file extension.");
        }
    }

    [Fact]
    public void TestLoadFilePassesFilename()
    {
        string filename = $"error-{Path.GetRandomFileName()}.polar";
        string path = Path.Join(Path.GetTempPath(), filename);
        var tempFile = File.Open(path, FileMode.Create);
        tempFile.WriteByte((byte)';');
        tempFile.Close();
        try
        {
            var polar = new Polar();
            var exception = Assert.Throws<OsoException>(() => polar.LoadFiles(path));
            Assert.StartsWith($"Unrecognized token: did not expect to find the token ';' at line 1, column 1 of file {path}:", exception.Message);
        }
        catch (ThrowsException)
        {
            throw new XunitException("Failed to pass filename across FFI boundary.");
        }
        File.Delete(path);
    }

    [Fact]
    public void TestLoadFileIdempotent()
    {
        var polar = new Polar();
        var path = Path.Join(TestPath, "Resources", "test.polar");
        polar.LoadFiles(path);
        var exception = Assert.Throws<OsoException>(() => polar.LoadFiles(path));
        Assert.Equal("Cannot load additional Polar code -- all Polar code must be loaded at the same time.", exception.Message);
        List<Dictionary<string, object>> expected = new()
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } },
            new() { { "x", 3 } },
        };
        try
        {
            Assert.Equal(expected, polar.NewQuery("f(x)", 0).Results, new ResultsComparer());
        }
        catch (XunitException ex)
        {
            throw new Exception(ex.GetType().ToString() + "loadFile behavior is not idempotent.");
        }
    }

    [Fact]
    public void TestLoadMultipleFiles()
    {
        var polar = new Polar();

        var path1 = Path.Join(TestPath, "Resources", "test.polar");
        var path2 = Path.Join(TestPath, "Resources", "test2.polar");
        polar.LoadFiles(new[] { path1, path2 });
        List<Dictionary<string, object>> expected = new()
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } },
            new() { { "x", 3 } },
        };
        Assert.Equal(expected, polar.NewQuery("f(x)", 0).Results, new ResultsComparer());
        Assert.Equal(expected, polar.NewQuery("g(x)", 0).Results, new ResultsComparer());
    }

    [Fact]
    public void TestLoadMultipleFilesSameNameDifferentPath()
    {
        var polar = new Polar();
        var path1 = Path.Join(TestPath, "Resources", "test.polar");
        var path2 = Path.Join(TestPath, "Resources", "other", "test.polar");
        polar.LoadFiles(path1, path2);
        List<Dictionary<string, object>> expected = new()
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } },
            new() { { "x", 3 } },
        };
        Assert.Equal(expected, polar.NewQuery("f(x)", 0).Results, new ResultsComparer());
        Assert.Equal(expected, polar.NewQuery("g(x)", 0).Results, new ResultsComparer());
    }

    [Fact]
    public void TestClearRules()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass));
        polar.LoadFiles(Path.Join(TestPath, "Resources", "test.polar"));
        List<Dictionary<string, object>> expected = new()
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } },
            new() { { "x", 3 } },
        };
        Assert.Equal(expected, polar.NewQuery("f(x)", 0).Results, new ResultsComparer());
        polar.ClearRules();
        var exception = Assert.Throws<OsoException>(() => polar.NewQuery("f(x)", 0));
        Assert.Equal("Query for undefined rule `f`", exception.Message);

        // make sure classes are still registered
        Assert.NotEmpty(polar.NewQuery("x = new MyClass(\"test\", 1)", 0).Results);
    }

    public class Foo
    {
        public string foo;

        public Foo()
        {
            this.foo = "foo";
        }
    }

    [Fact]
    public void TestLookupErrors()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(Foo), "Foo");
        Assert.Equal(new List<Dictionary<string, object>>(), polar.NewQuery("new Foo() = {bar: \"bar\"}", 0).Results, new ResultsComparer());
        var exception = Assert.Throws<OsoException>(() => polar.NewQuery("new Foo().bar = \"bar\"", 0));
    }

    [Fact]
    public void TestUnboundVariable()
    {
        var polar = new Polar();
        polar.LoadStr("rule(_x, y) if y = 1;");
        var result = polar.NewQuery("rule(x, y)", 0).Results.First();
        Assert.True(result["x"] is Variable);
        Assert.Equal(result["y"], 1);
    }

    [Fact]
    public void TestReturnNull()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(MyClass), "MyClass");
        polar.LoadStr("f(x) if x.MyReturnNull() = nil;");
        Assert.NotEmpty(polar.QueryRule("f", new MyClass("test", 1)).Results);

        polar.ClearRules();

        polar.LoadStr("g(x) if x.MyReturnNull().BadCall() = 1;");
        Assert.Throws<NullReferenceException>(() => polar.QueryRule("g", new MyClass("test", 1)));
    }
    #endregion
    #region Test Oso
    public class NotIterable { }

    public class BarIterator : IEnumerable<int>
    {
        private List<int> List;
        public BarIterator(List<object> list)
        {
            this.List = list.Cast<int>().ToList();
        }

        // code for data structure
        public int Sum() => this.List.Aggregate((x, y) => x + y);

        IEnumerator IEnumerable.GetEnumerator() => List.GetEnumerator();
        public IEnumerator<int> GetEnumerator() => List.GetEnumerator();
    }

    [Fact]
    public void TestIterators()
    {
        var polar = new Polar();
        // non iterables throw exception
        polar.RegisterClass(typeof(NotIterable), "NotIterable");
        var exception = Assert.Throws<OsoException>(() => polar.NewQuery("x in new NotIterable()", 0));
        Assert.Equal("Invalid iterator: value Oso.Tests.PolarTests+NotIterable of type Oso.Tests.PolarTests+NotIterable is not iterable", exception.Message);

        // custom iterators work
        polar.RegisterClass(typeof(BarIterator), "BarIterator");

        List<Dictionary<string, object>> expected1 = new()
        {
            new() { { "x", 1 } },
            new() { { "x", 2 } },
            new() { { "x", 3 } },
        };
        Assert.Equal(expected1, polar.NewQuery("x in new BarIterator([1, 2, 3])", 0).Results, new ResultsComparer());

        List<Dictionary<string, object>> expected2 = new()
        {
            new() { { "x", 6 } },
        };
        Assert.Equal(expected2, polar.NewQuery("x = new BarIterator([1, 2, 3]).Sum()", 0).Results, new ResultsComparer());
    }

    [Fact]
    public void TestExpressionGt()
    {
        var polar = new Polar();
        // GIVEN
        polar.LoadStr("f(x) if x > 2;");
        // WHEN
        var query = polar.NewQuery("f(x)", true, 0);
        IEnumerable<Dictionary<string, object>> results = query.Results.ToList();
        // THEN
        Assert.Single(results);
        Dictionary<string, object> result = results.First();
        Assert.Single(result);
        object expr = result["x"];
        Expression expected = new Expression(Operator.And, new() { new Expression(Operator.Gt, new() { new Variable("_this"), 2 }) });
        Assert.Equal(expected, expr);
    }

    [Fact]
    public void TestPartialUnification()
    {
        var polar = new Polar();
        // GIVEN
        polar.LoadStr("f(x, y) if x = y;");
        Variable x = new Variable("x");
        Variable y = new Variable("y");

        // WHEN
        var query = polar.QueryRule("f", x, y);
        var results = query.Results;

        // THEN
        Assert.Single(results);
        Dictionary<string, object> bindings = query.Results.First();
        Assert.Equal(y, bindings["x"]);
        Assert.Equal(x, bindings["y"]);
    }

    [Fact]
    public void TestPartial()
    {
        // GIVEN
        var polar = new Polar();
        polar.LoadStr("f(1); f(x) if x = 1 and x = 2;");

        // WHEN
        Predicate rule = new Predicate("f", new List<Variable>() { new Variable("x") });
        var query = polar.NewQuery(rule, true);
        Assert.Single(query.Results);
        Assert.Equal(1, query.Results.First()["x"]);

        polar.ClearRules();

        polar.LoadStr("g(x) if x.bar = 1 and x.baz = 2;");

        Predicate gRule = new Predicate("g", new List<Variable> { new("x") });
        var query2 = polar.NewQuery(gRule, true);
        Assert.Single(query2.Results);
        Expression expr = (Expression)query2.Results.First()["x"];
        List<object> args = (List<Object>)unwrapAnd(expr);
        Assert.Equal(2, args.Count);
        Assert.Equal(
            args[0],
            new Expression(
                Operator.Unify, new List<object> { 1, new Expression(Operator.Dot, new List<object> { new Variable("_this"), "bar" }) }));
        Assert.Equal(
            args[1],
            new Expression(
                Operator.Unify, new List<object> { 2, new Expression(Operator.Dot, new List<object> { new Variable("_this"), "baz" }) }));
    }

    private object unwrapAnd(Expression expression)
    {
        Assert.Equal(expression.Operator, Operator.And);
        return (expression.Args.Count() == 1)
            ? expression.Args.Single()
            : expression.Args;
    }

    public class User { }

    public class Post { }

    [Fact]
    public void TestPartialConstraint()
    {
        var polar = new Polar();
        polar.RegisterClass(typeof(User), "User");
        polar.RegisterClass(typeof(Post), "Post");
        polar.LoadStr("f(x: User) if x.User = 1; f(x: Post) if x.Post = 1;");

        Variable x = new Variable("x");
        Predicate rule = new Predicate("f", new List<object>() { x });
        var results =
            polar.NewQuery(rule, new Dictionary<string, object>() { { "x", new TypeConstraint(x, "User") } }, true).Results.ToList();

        Assert.Single(results);

        List<object> andArgs = (List<object>)unwrapAnd((Expression)results[0]["x"]);
        Assert.Equal(2, andArgs.Count());
        Assert.Equal(
            new Expression(Operator.Isa, new() { new Variable("_this"), new Pattern("User", new()) }),
            andArgs[0]);
        Assert.Equal(
            new Expression(
                Operator.Unify, new List<object> { 1, new Expression(Operator.Dot, new() { new Variable("_this"), "User" }) }),
            andArgs[1]);
    }

    [Fact]
    public void TestUnexpectedExpression()
    {
        var polar = new Polar();
        polar.LoadStr("f(x) if x > 2;");

        try
        {
            var exception = Assert.Throws<UnexpectedPolarTypeException>(() => polar.NewQuery("f(x)", 0));
        }
        catch (ThrowsException)
        {
            throw new XunitException("Expected inline query to fail but it didn't.");
        }
    }

    public class Bar : Foo { }

    public class Baz : Bar { }

    public class Bad { }

    [Fact]
    public void TestRuleTypes()
    {
        var polar = new Polar();
        // NOTE: keep this order of registering classes--confirms that MROs are added at the correct
        // time
        polar.RegisterClass(typeof(Baz), "Baz");
        polar.RegisterClass(typeof(Bar), "Bar");
        polar.RegisterClass(typeof(Foo), "Foo");
        polar.RegisterClass(typeof(Bad), "Bad");

        const string policy1 =
            "type f(_x: Integer);"
                + "f(1);"
                + "type f(_x: Foo);"
                + "type f(_x: Foo, _y: Bar);"
                + "f(_x: Bar);"
                + "f(_x: Baz);";

        polar.LoadStr(policy1);

        polar.ClearRules();

        try
        {
            var exception = Assert.Throws<OsoException>(() => polar.LoadStr(policy1 + "f(_x: Bad);"));
            Assert.StartsWith("Invalid rule: f(_x: Bad{}); Must match on", exception.Message);
        }
        catch (ThrowsException)
        {
            throw new XunitException("Expected rule type validation error.");
        }

        //  Test with fields
        const string policy2 = "type f(_x: Foo{id: 1});" + "f(_x: Bar{id: 1});" + "f(_x: Baz{id: 1});";

        polar.LoadStr(policy2);

        polar.ClearRules();

        try
        {
            var exception = Assert.Throws<OsoException>(() => polar.LoadStr(policy2 + "f(_x: Baz);"));
            Assert.StartsWith("Invalid rule: f(_x: Baz{}); Must match on", exception.Message);
        }
        catch (ThrowsException)
        {
            throw new XunitException("Expected rule type validation error.");
        }

        try
        {
            // Test invalid rule type
            var exception = Assert.Throws<OsoException>(() => polar.LoadStr("type f(x: Foo, x.baz);"));
            Assert.StartsWith("Invalid rule type: f(x: Foo{}, _value_1)", exception.Message);
        }
        catch (ThrowsException)
        {
            throw new XunitException("Expected rule type validation error.");
        }
    }
    #endregion
}
internal class ResultsComparer : IEqualityComparer<IEnumerable<Dictionary<string, object>>>
{
    public bool Equals(IEnumerable<Dictionary<string, object>>? x, IEnumerable<Dictionary<string, object>>? y)
    {
        if (x == null || y == null) return false;

        foreach (var (xResult, yResult) in x.Zip(y))
        {
            if (!xResult.SequenceEqual(yResult)) return false;
        }
        return true;
    }

    public int GetHashCode([DisallowNull] IEnumerable<Dictionary<string, object>> obj)
    {
        throw new NotImplementedException();
    }
}