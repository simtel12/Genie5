using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 script-language parity for the expression evaluator (Group A of the
/// parity audit). Locks in: match() = exact case-sensitive equality (not the
/// old case-insensitive substring), the `eq` / `&lt;&gt;` operators, and the
/// instr/instring/substring/defined aliases.
/// </summary>
public class ScriptExpressionTests
{
    private static ScriptInstance NewInst()
    {
        var inst = new ScriptInstance();
        inst.Vars["foo"] = "bar";
        inst.Vars["empty"] = "";
        inst.DollarStack.Push(new string[10]);
        return inst;
    }

    [Theory]
    // match(): exact equality, case-sensitive (Ordinal) — NOT substring
    [InlineData("match(\"abc\",\"abc\")", true)]
    [InlineData("match(\"abc\",\"ab\")",  false)]   // was TRUE under the substring bug
    [InlineData("match(\"abc\",\"ABC\")", false)]   // case-sensitive
    [InlineData("match(\"ab\",\"abc\")",  false)]
    // `eq` word operator ≡ `=`
    [InlineData("5 eq 5", true)]
    [InlineData("5 eq 6", false)]
    [InlineData("\"abc\" eq \"abc\"", true)]
    [InlineData("\"abc\" eq \"ABC\"", false)]
    // `<>` operator ≡ `!=`
    [InlineData("5 <> 6", true)]
    [InlineData("5 <> 5", false)]
    [InlineData("\"a\" <> \"b\"", true)]
    // regression guard: inserting `<>` must not break `<` / `<=` / `>` / `>=`
    [InlineData("3 < 5",  true)]
    [InlineData("5 < 3",  false)]
    [InlineData("5 <= 5", true)]
    [InlineData("6 >= 7", false)]
    [InlineData("8 > 2",  true)]
    // instr / instring are boolean `contains` aliases (G4-faithful, not a position)
    [InlineData("instr(\"hello world\",\"world\")", true)]
    [InlineData("instring(\"hello\",\"xyz\")",      false)]
    [InlineData("contains(\"hello\",\"ell\")",      true)]
    // defined alias of def
    [InlineData("defined(foo)",   true)]
    [InlineData("defined(empty)", true)]    // #129: EXISTS (even set to "") ⇒ defined (Genie 4 ContainsKey)
    [InlineData("defined(nope)",  false)]   // never set ⇒ not defined
    [InlineData("def(foo)",       true)]
    // Group B: string predicates are case-SENSITIVE (Genie 4 parity)
    [InlineData("contains(\"Hello\",\"hello\")", false)]   // case mismatch ⇒ no match
    [InlineData("contains(\"Hello\",\"Hel\")",   true)]    // same-case still matches (see Group A block)
    [InlineData("startswith(\"Hello\",\"hel\")", false)]
    [InlineData("startswith(\"Hello\",\"Hel\")", true)]
    [InlineData("endswith(\"Hello\",\"LO\")",    false)]
    [InlineData("endswith(\"Hello\",\"lo\")",    true)]
    // Group B: indexof is 1-based ⇒ not-found is 0 (falsy); the GenieHunter/
    // hunt.cmd idiom `if !indexof(hay, needle)` means "needle absent".
    [InlineData("!indexof(\"longsword\",\"$\")", true)]    // no $  ⇒ absent ⇒ fires
    [InlineData("!indexof(\"a$b\",\"$\")",       false)]   // $ present ⇒ doesn't fire
    [InlineData("indexof(\"Hello\",\"h\")",      false)]   // case-sensitive miss ⇒ 0 ⇒ falsy
    public void EvalBool_matches_Genie4(string expr, bool expected)
        => Assert.Equal(expected, ScriptExpression.EvalBool(expr, NewInst()));

    [Theory]
    // Group B: indexof / lastindexof are 1-based and case-sensitive (Genie 4).
    [InlineData("indexof(\"hello\",\"ell\")",   "2")]   // 0-based 1 → 1-based 2
    [InlineData("indexof(\"hello\",\"h\")",     "1")]
    [InlineData("indexof(\"hello\",\"xyz\")",   "0")]   // not found ⇒ 0
    [InlineData("indexof(\"Hello\",\"h\")",     "0")]   // case-sensitive miss ⇒ 0
    [InlineData("lastindexof(\"hello\",\"l\")", "4")]   // last l: 0-based 3 → 4
    [InlineData("lastindexof(\"hello\",\"z\")", "0")]
    public void Eval_indexof_is_1based_and_caseSensitive(string expr, string expected)
        => Assert.Equal(expected, ScriptExpression.Eval(expr, NewInst())?.ToString());

    [Theory]
    [InlineData("substring(\"hello\",1,3)", "ell")]
    [InlineData("substr(\"hello\",1,3)",    "ell")]
    [InlineData("equipment", "equipment")]  // `eq` must NOT fire inside an identifier
    public void Eval_string_results(string expr, string expected)
        => Assert.Equal(expected, ScriptExpression.Eval(expr, NewInst())?.ToString());
}
