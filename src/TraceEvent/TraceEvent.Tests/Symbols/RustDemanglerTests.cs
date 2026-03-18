using Microsoft.Diagnostics.Symbols;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class RustDemanglerTests : TestBase
    {
        private readonly RustDemangler _demangler = new RustDemangler();

        public RustDemanglerTests(ITestOutputHelper output)
            : base(output)
        {
        }

        #region Null and Invalid Input

        [Fact]
        public void WhenInputIsNullThenReturnsNull()
        {
            Assert.Null(_demangler.Demangle(null));
        }

        [Fact]
        public void WhenInputIsEmptyThenReturnsNull()
        {
            Assert.Null(_demangler.Demangle(""));
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("main")]
        [InlineData("_")]
        [InlineData("_R")]
        [InlineData("R")]
        [InlineData("_r")]
        [InlineData("XR")]
        [InlineData("__Z3foov")]
        [InlineData("_Z3foov")]
        public void WhenInputIsNotRustV0MangledThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_R!")]
        [InlineData("_RZ")]
        [InlineData("_R$")]
        [InlineData("_Rinvalid")]
        public void WhenInputIsMalformedRustMangledNameThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        #endregion

        #region Crate Roots

        [Theory]
        [InlineData("_RC4test", "test")]
        [InlineData("_RC3std", "std")]
        [InlineData("_RC5alloc", "alloc")]
        [InlineData("_RC4core", "core")]
        [InlineData("_RC7mycrate", "mycrate")]
        public void WhenInputIsCrateRootThenReturnsCrateName(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RCs_4test", "test")]
        [InlineData("_RCs0_4test", "test")]
        [InlineData("_RCs1_4test", "test")]
        public void WhenCrateHasDisambiguatorThenDisambiguatorIsOmitted(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Simple Functions

        [Theory]
        [InlineData("_RNvC4test3foo", "test::foo")]
        [InlineData("_RNvC3std5hello", "std::hello")]
        [InlineData("_RNvC4test4main", "test::main")]
        [InlineData("_RNvC5alloc6malloc", "alloc::malloc")]
        public void WhenInputIsSimpleFunctionThenReturnsCrateAndFunction(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Nested Paths

        [Theory]
        [InlineData("_RNvNvC4test3foo3bar", "test::foo::bar")]
        [InlineData("_RNvNvNvC4test1a1b1c", "test::a::b::c")]
        [InlineData("_RNvNvNvNvC4test1a1b1c1d", "test::a::b::c::d")]
        [InlineData("_RNvNvC3std3mem4swap", "std::mem::swap")]
        public void WhenInputIsNestedPathThenReturnsFullyQualifiedName(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Inherent Impl Blocks

        [Fact]
        public void WhenInputIsInherentImplThenReturnsAngleBracketedType()
        {
            // M disambiguator impl-path type → <Type>
            // <test::MyType>
            string result = _demangler.Demangle("_RMs_C4testNtC4test6MyType");
            Assert.Equal("<test::MyType>", result);
        }

        [Fact]
        public void WhenInputIsInherentImplMethodThenReturnsTypeWithMethod()
        {
            // N v (M disambiguator impl-path type) method → <Type>::method
            string result = _demangler.Demangle("_RNvMs_C4testNtC4test6MyType6method");
            Assert.Equal("<test::MyType>::method", result);
        }

        [Fact]
        public void WhenInputIsInherentImplOnPrimitiveThenFormatsCorrectly()
        {
            // <i32>::method — M with type = i32 (basic type 'l')
            string result = _demangler.Demangle("_RNvMs_C4testl6method");
            Assert.Equal("<i32>::method", result);
        }

        #endregion

        #region Trait Impl Blocks

        [Fact]
        public void WhenInputIsTraitImplWithXTagThenReturnsTypeAsTrait()
        {
            // X disambiguator impl-path type trait-path → <Type as Trait>
            // <i32 as test::MyTrait>::method
            string result = _demangler.Demangle("_RNvXs_C4testlNtC4test7MyTrait6method");
            Assert.Equal("<i32 as test::MyTrait>::method", result);
        }

        [Fact]
        public void WhenInputIsTraitRefWithYTagThenReturnsTypeAsTrait()
        {
            // Y type path → <Type as Path>
            // <i32 as test::MyTrait>::method
            string result = _demangler.Demangle("_RNvYlNtC4test7MyTrait6method");
            Assert.Equal("<i32 as test::MyTrait>::method", result);
        }

        [Fact]
        public void WhenInputIsTraitImplOnStrThenFormatsCorrectly()
        {
            // <str as test::MyTrait>::method
            string result = _demangler.Demangle("_RNvXs_C4testeNtC4test7MyTrait6method");
            Assert.Equal("<str as test::MyTrait>::method", result);
        }

        #endregion

        #region Generic Arguments

        [Fact]
        public void WhenInputHasSingleGenericArgThenUsesTurbofishSyntax()
        {
            // test::foo::<i32>
            Assert.Equal("test::foo::<i32>", _demangler.Demangle("_RINvC4test3foolE"));
        }

        [Fact]
        public void WhenInputHasMultipleGenericArgsThenSeparatesWithComma()
        {
            // test::foo::<i32, u8>
            Assert.Equal("test::foo::<i32, u8>", _demangler.Demangle("_RINvC4test3foolhE"));
        }

        [Fact]
        public void WhenInputHasThreeGenericArgsThenFormatsCorrectly()
        {
            // test::foo::<i32, u8, bool>
            Assert.Equal("test::foo::<i32, u8, bool>", _demangler.Demangle("_RINvC4test3foolhbE"));
        }

        [Fact]
        public void WhenInputHasEmptyGenericArgsThenOmitsAngleBrackets()
        {
            // I path E with zero args → just the path without <>
            Assert.Equal("test::foo", _demangler.Demangle("_RINvC4test3fooE"));
        }

        [Fact]
        public void WhenGenericArgIsNestedThenInnerUsesNonTurbofishSyntax()
        {
            // test::foo::<test::Option<i32>>
            // Outer I is inValue=true (turbofish), inner I is inValue=false (no turbofish)
            string result = _demangler.Demangle("_RINvC4test3fooINtC4test6OptionlEE");
            Assert.Equal("test::foo::<test::Option<i32>>", result);
        }

        [Fact]
        public void WhenGenericArgIsLifetimeThenShowsPlaceholder()
        {
            // Lifetime generic arg → '_
            string result = _demangler.Demangle("_RINvC4test3fooL_E");
            Assert.Equal("test::foo::<'_>", result);
        }

        #endregion

        #region Basic Types

        [Theory]
        [InlineData("_RINvC4test1faE", "test::f::<i8>")]
        [InlineData("_RINvC4test1fbE", "test::f::<bool>")]
        [InlineData("_RINvC4test1fcE", "test::f::<char>")]
        [InlineData("_RINvC4test1fdE", "test::f::<f64>")]
        [InlineData("_RINvC4test1feE", "test::f::<str>")]
        [InlineData("_RINvC4test1ffE", "test::f::<f32>")]
        [InlineData("_RINvC4test1fhE", "test::f::<u8>")]
        [InlineData("_RINvC4test1fiE", "test::f::<isize>")]
        [InlineData("_RINvC4test1fjE", "test::f::<usize>")]
        [InlineData("_RINvC4test1flE", "test::f::<i32>")]
        [InlineData("_RINvC4test1fmE", "test::f::<u32>")]
        [InlineData("_RINvC4test1fnE", "test::f::<i128>")]
        [InlineData("_RINvC4test1foE", "test::f::<u128>")]
        [InlineData("_RINvC4test1fpE", "test::f::<_>")]
        [InlineData("_RINvC4test1fsE", "test::f::<i16>")]
        [InlineData("_RINvC4test1ftE", "test::f::<u16>")]
        [InlineData("_RINvC4test1fuE", "test::f::<()>")]
        [InlineData("_RINvC4test1fvE", "test::f::<...>")]
        [InlineData("_RINvC4test1fxE", "test::f::<i64>")]
        [InlineData("_RINvC4test1fyE", "test::f::<u64>")]
        [InlineData("_RINvC4test1fzE", "test::f::<!>")]
        public void WhenGenericArgIsBasicTypeThenFormatsCorrectly(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Array Types

        [Fact]
        public void WhenTypeIsArrayOfI32WithSize10ThenFormatsAsBracketedType()
        {
            // [u8; 10] — A + h (u8) + const(j=usize, hex "a"=10)
            Assert.Equal("test::f::<[u8; 10]>", _demangler.Demangle("_RINvC4test1fAhja_E"));
        }

        [Fact]
        public void WhenTypeIsArrayWithSize0ThenFormatsCorrectly()
        {
            // [u8; 0] — A + h + const(j=usize, empty hex)
            Assert.Equal("test::f::<[u8; 0]>", _demangler.Demangle("_RINvC4test1fAhj_E"));
        }

        [Fact]
        public void WhenTypeIsArrayWithSize256ThenFormatsCorrectly()
        {
            // [i32; 256] — A + l + const(j=usize, hex "100"=256)
            Assert.Equal("test::f::<[i32; 256]>", _demangler.Demangle("_RINvC4test1fAlj100_E"));
        }

        #endregion

        #region Slice Types

        [Theory]
        [InlineData("_RINvC4test1fSlE", "test::f::<[i32]>")]
        [InlineData("_RINvC4test1fShE", "test::f::<[u8]>")]
        [InlineData("_RINvC4test1fSbE", "test::f::<[bool]>")]
        public void WhenTypeIsSliceThenFormatsAsBracketedType(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Tuple Types

        [Fact]
        public void WhenTypeIsEmptyTupleThenFormatsAsUnitTuple()
        {
            // T E → ()
            Assert.Equal("test::f::<()>", _demangler.Demangle("_RINvC4test1fTEE"));
        }

        [Fact]
        public void WhenTypeIsSingleElementTupleThenIncludesTrailingComma()
        {
            // T l E → (i32,) — single-element tuple has trailing comma
            Assert.Equal("test::f::<(i32,)>", _demangler.Demangle("_RINvC4test1fTlEE"));
        }

        [Fact]
        public void WhenTypeIsTwoElementTupleThenSeparatesWithComma()
        {
            // T l h E → (i32, u8)
            Assert.Equal("test::f::<(i32, u8)>", _demangler.Demangle("_RINvC4test1fTlhEE"));
        }

        [Fact]
        public void WhenTypeIsThreeElementTupleThenFormatsCorrectly()
        {
            // T l h b E → (i32, u8, bool)
            Assert.Equal("test::f::<(i32, u8, bool)>", _demangler.Demangle("_RINvC4test1fTlhbEE"));
        }

        #endregion

        #region Reference Types

        [Theory]
        [InlineData("_RINvC4test1fRlE", "test::f::<&i32>")]
        [InlineData("_RINvC4test1fReE", "test::f::<&str>")]
        [InlineData("_RINvC4test1fRbE", "test::f::<&bool>")]
        public void WhenTypeIsReferenceThenFormatsWithAmpersand(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RINvC4test1fQlE", "test::f::<&mut i32>")]
        [InlineData("_RINvC4test1fQeE", "test::f::<&mut str>")]
        public void WhenTypeIsMutableReferenceThenFormatsWithAmpersandMut(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Fact]
        public void WhenReferenceHasLifetimeThenLifetimeIsSkipped()
        {
            // R L_ l → &i32 (lifetime skipped)
            Assert.Equal("test::f::<&i32>", _demangler.Demangle("_RINvC4test1fRL_lE"));
        }

        [Fact]
        public void WhenMutableReferenceHasLifetimeThenLifetimeIsSkipped()
        {
            // Q L_ l → &mut i32
            Assert.Equal("test::f::<&mut i32>", _demangler.Demangle("_RINvC4test1fQL_lE"));
        }

        #endregion

        #region Raw Pointer Types

        [Theory]
        [InlineData("_RINvC4test1fPlE", "test::f::<*const i32>")]
        [InlineData("_RINvC4test1fPhE", "test::f::<*const u8>")]
        [InlineData("_RINvC4test1fPuE", "test::f::<*const ()>")]
        public void WhenTypeIsConstRawPointerThenFormatsCorrectly(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RINvC4test1fOlE", "test::f::<*mut i32>")]
        [InlineData("_RINvC4test1fOhE", "test::f::<*mut u8>")]
        public void WhenTypeIsMutRawPointerThenFormatsCorrectly(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Function Pointer Types

        [Fact]
        public void WhenTypeIsFnPointerWithUnitReturnThenReturnIsOmitted()
        {
            // fn(i32) — F l E u → return "()" suppressed
            Assert.Equal("test::f::<fn(i32)>", _demangler.Demangle("_RINvC4test1fFlEuE"));
        }

        [Fact]
        public void WhenTypeIsFnPointerWithReturnThenShowsArrow()
        {
            // fn(i32) -> u8
            Assert.Equal("test::f::<fn(i32) -> u8>", _demangler.Demangle("_RINvC4test1fFlEhE"));
        }

        [Fact]
        public void WhenTypeIsFnPointerWithNoParamsThenFormatsCorrectly()
        {
            // fn()
            Assert.Equal("test::f::<fn()>", _demangler.Demangle("_RINvC4test1fFEuE"));
        }

        [Fact]
        public void WhenTypeIsFnPointerWithMultipleParamsThenSeparatesWithComma()
        {
            // fn(i32, u8) -> bool
            Assert.Equal("test::f::<fn(i32, u8) -> bool>", _demangler.Demangle("_RINvC4test1fFlhEbE"));
        }

        [Fact]
        public void WhenTypeIsUnsafeFnPointerThenShowsUnsafe()
        {
            // unsafe fn(i32)
            Assert.Equal("test::f::<unsafe fn(i32)>", _demangler.Demangle("_RINvC4test1fFUlEuE"));
        }

        [Fact]
        public void WhenTypeIsExternCFnPointerThenShowsExternC()
        {
            // extern "C" fn(i32)
            Assert.Equal("test::f::<extern \"C\" fn(i32)>", _demangler.Demangle("_RINvC4test1fFKClEuE"));
        }

        [Fact]
        public void WhenTypeIsUnsafeExternCFnPointerThenShowsBoth()
        {
            // unsafe extern "C" fn(i32, u8) -> bool
            Assert.Equal(
                "test::f::<unsafe extern \"C\" fn(i32, u8) -> bool>",
                _demangler.Demangle("_RINvC4test1fFUKClhEbE"));
        }

        [Fact]
        public void WhenTypeIsFnPointerWithNoReturnThenFormatsCorrectly()
        {
            // fn() -> u8 (no params, non-unit return)
            Assert.Equal("test::f::<fn() -> u8>", _demangler.Demangle("_RINvC4test1fFEhE"));
        }

        [Fact]
        public void WhenFnTypeHasHigherRankedLifetimeBinderThenParsesCorrectly()
        {
            // for<'a> fn(&i32) -> bool — G0_ (1 bound lifetime) F Rl E b
            // G0_ = binder with 1 lifetime, F = fn sig, Rl = &i32 param, E = end params, b = bool return
            Assert.Equal(
                "test::f::<fn(&i32) -> bool>",
                _demangler.Demangle("_RINvC4test1fG0_FRlEbE"));
        }

        [Fact]
        public void WhenFnTypeHasBinderWithZeroLifetimesThenParsesCorrectly()
        {
            // G_ (0 bound lifetimes) F l E u → fn(i32)
            Assert.Equal("test::f::<fn(i32)>", _demangler.Demangle("_RINvC4test1fG_FlEuE"));
        }

        [Fact]
        public void WhenFnSigHasInlineBinderThenParsesCorrectly()
        {
            // F G_ RL0_e E b → for<'a> fn(&str) -> bool — binder inside fn-sig
            Assert.Equal("test::f::<fn(&str) -> bool>",
                _demangler.Demangle("_RINvC4test1fFG_RL0_eEbE"));
        }

        [Fact]
        public void WhenFnSigHasBinderWithMultipleLifetimesThenParsesCorrectly()
        {
            // F G0_ RL0_h E u → for<'a> fn(&u8) — binder with 1+1=2 lifetimes inside fn-sig
            Assert.Equal("test::f::<fn(&u8)>",
                _demangler.Demangle("_RINvC4test1fFG0_RL0_hEuE"));
        }

        #endregion

        #region Dyn Trait Objects

        [Fact]
        public void WhenTypeIsDynTraitThenFormatsDynPrefix()
        {
            // dyn test::Trait — D NtC4test5Trait E L_
            Assert.Equal(
                "test::f::<dyn test::Trait>",
                _demangler.Demangle("_RINvC4test1fDNtC4test5TraitEL_E"));
        }

        [Fact]
        public void WhenTypeIsDynWithMultipleTraitsThenJoinsWithPlus()
        {
            // dyn test::Trait + test::Send
            Assert.Equal(
                "test::f::<dyn test::Trait + test::Send>",
                _demangler.Demangle("_RINvC4test1fDNtC4test5TraitNtC4test4SendEL_E"));
        }

        [Fact]
        public void WhenTypeIsDynWithAssociatedTypeThenShowsBinding()
        {
            // dyn test::Iterator<Item = i32>
            Assert.Equal(
                "test::f::<dyn test::Iterator<Item = i32>>",
                _demangler.Demangle("_RINvC4test1fDNtC4test8Iteratorp4ItemlEL_E"));
        }

        [Fact]
        public void WhenTypeIsDynWithNoTraitsThenShowsBare()
        {
            // dyn (with empty trait list)
            Assert.Equal("test::f::<dyn>", _demangler.Demangle("_RINvC4test1fDEL_E"));
        }

        [Fact]
        public void WhenDynBoundsIsMissingRequiredLifetimeThenReturnsNull()
        {
            // D NtC4test5Trait E (missing L_ lifetime) → should fail
            Assert.Null(_demangler.Demangle("_RINvC4test1fDNtC4test5TraitEE"));
        }

        #endregion

        #region Const Generic Values

        [Theory]
        [InlineData("_RINvC4test1fKl5_E", "test::f::<5>")]
        [InlineData("_RINvC4test1fKl_E", "test::f::<0>")]
        [InlineData("_RINvC4test1fKla_E", "test::f::<10>")]
        [InlineData("_RINvC4test1fKlff_E", "test::f::<255>")]
        public void WhenConstGenericIsIntegerThenFormatsAsDecimal(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Fact]
        public void WhenConstGenericIsNegativeIntegerThenShowsMinusSign()
        {
            // -3 as i32 const
            Assert.Equal("test::f::<-3>", _demangler.Demangle("_RINvC4test1fKln3_E"));
        }

        [Fact]
        public void WhenConstGenericIsNegativeZeroThenShowsMinusZero()
        {
            // -0 as i32 const
            Assert.Equal("test::f::<-0>", _demangler.Demangle("_RINvC4test1fKln_E"));
        }

        [Theory]
        [InlineData("_RINvC4test1fKb1_E", "test::f::<true>")]
        [InlineData("_RINvC4test1fKb0_E", "test::f::<false>")]
        [InlineData("_RINvC4test1fKb_E", "test::f::<false>")]
        public void WhenConstGenericIsBoolThenFormatsAsTrueOrFalse(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RINvC4test1fKb2_E")]
        [InlineData("_RINvC4test1fKbff_E")]
        [InlineData("_RINvC4test1fKbn1_E")]
        public void WhenConstGenericIsBoolWithInvalidValueThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RINvC4test1fKc41_E", "test::f::<'A'>")]
        [InlineData("_RINvC4test1fKc61_E", "test::f::<'a'>")]
        [InlineData("_RINvC4test1fKc30_E", "test::f::<'0'>")]
        public void WhenConstGenericIsCharThenFormatsWithSingleQuotes(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Fact]
        public void WhenConstGenericIsCharWithNegativeThenReturnsNull()
        {
            // Negative prefix is not valid for char per RFC 2603.
            Assert.Null(_demangler.Demangle("_RINvC4test1fKcn41_E"));
        }

        [Theory]
        [InlineData("_RINvC4test1fKc27_E", "test::f::<'\\''>")]      // single quote U+0027 → '\''
        [InlineData("_RINvC4test1fKc5c_E", "test::f::<'\\\\'>")]     // backslash U+005C → '\\'
        [InlineData("_RINvC4test1fKc0a_E", "test::f::<'\\n'>")]      // newline U+000A → '\n'
        [InlineData("_RINvC4test1fKc0d_E", "test::f::<'\\r'>")]      // carriage return U+000D → '\r'
        [InlineData("_RINvC4test1fKc9_E", "test::f::<'\\t'>")]       // tab U+0009 → '\t'
        [InlineData("_RINvC4test1fKc1_E", "test::f::<'\\u{1}'>")]    // control char U+0001 → '\u{1}'
        public void WhenConstGenericIsSpecialCharThenFormatsWithEscaping(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RINvC4test1fKhn1_E")]   // negative u8
        [InlineData("_RINvC4test1fKjn1_E")]   // negative usize
        [InlineData("_RINvC4test1fKtn1_E")]   // negative u16
        [InlineData("_RINvC4test1fKyn1_E")]   // negative u64
        [InlineData("_RINvC4test1fKon1_E")]   // negative u128
        public void WhenConstGenericIsNegativeUnsignedThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Fact]
        public void WhenConstGenericIsNegativeSignedThenSucceeds()
        {
            // Negative i32 is valid — should still demangle
            Assert.Equal("test::f::<-1>", _demangler.Demangle("_RINvC4test1fKln1_E"));
        }

        [Fact]
        public void WhenConstGenericIsPlaceholderThenShowsUnderscore()
        {
            Assert.Equal("test::f::<_>", _demangler.Demangle("_RINvC4test1fKpE"));
        }

        [Fact]
        public void WhenConstGenericIsLargeHexThenFormatsAsHex()
        {
            // A value with more than 16 hex digits → shown as 0x prefix
            Assert.Equal(
                "test::f::<0x12345678901234567>",
                _demangler.Demangle("_RINvC4test1fKl12345678901234567_E"));
        }

        #endregion

        #region Backreferences

        [Fact]
        public void WhenTypeUsesBackrefToBasicTypeThenResolvesCorrectly()
        {
            // Two generic args: first is 'l' (i32), second is backref to the first
            // _RINvC4test3foolBc_E
            // Backref 'c_' → base62: 'c' = 12, + 1 = 13 → relative pos 13 → absolute pos 15 → 'l' = i32
            Assert.Equal("test::foo::<i32, i32>", _demangler.Demangle("_RINvC4test3foolBc_E"));
        }

        #endregion

        #region Closures

        [Fact]
        public void WhenNamespaceIsClosureWithEmptyNameThenShowsClosureBraces()
        {
            // {closure} — N + C + path + disambiguator + empty name
            Assert.Equal("test::foo::{closure}", _demangler.Demangle("_RNCNvC4test3foos_0"));
        }

        [Fact]
        public void WhenNamespaceIsClosureWithNameThenShowsClosureWithName()
        {
            // {closure:name}
            Assert.Equal(
                "test::foo::{closure:my_fun}",
                _demangler.Demangle("_RNCNvC4test3foos_6my_fun"));
        }

        [Fact]
        public void WhenClosureHasDifferentDisambiguatorThenStillShowsClosure()
        {
            // Different disambiguator value (1 → base62 "0_")
            Assert.Equal("test::foo::{closure}", _demangler.Demangle("_RNCNvC4test3foos0_0"));
        }

        #endregion

        #region Shims

        [Fact]
        public void WhenNamespaceIsShimWithEmptyNameThenShowsShimBraces()
        {
            Assert.Equal("test::foo::{shim}", _demangler.Demangle("_RNSNvC4test3foos_0"));
        }

        [Fact]
        public void WhenNamespaceIsShimWithNameThenShowsShimWithName()
        {
            Assert.Equal(
                "test::foo::{shim:drop}",
                _demangler.Demangle("_RNSNvC4test3foos_4drop"));
        }

        #endregion

        #region Punycode Identifiers

        [Fact]
        public void WhenIdentifierIsPunycodeEncodedThenDecodesToUnicode()
        {
            // Punycode for "café": basic="caf", delta="dma" → "caf_dma" (length 7)
            // u7caf_dma → decoded to "café"
            Assert.Equal("test::caf\u00e9", _demangler.Demangle("_RNvC4testu7caf_dma"));
        }

        [Fact]
        public void WhenIdentifierIsPunycodeWithNoBasicCharsThenDecodesCorrectly()
        {
            // Punycode for "α" (U+03B1): delta only = "mxa" (length 3)
            Assert.Equal("test::\u03b1", _demangler.Demangle("_RNvC4testu3mxa"));
        }

        [Fact]
        public void WhenPunycodeHasLongDeltaEncodingThenDoesNotOverflow()
        {
            // Punycode for "イポュ" (U+30A4, U+30DD, U+30E5): delta "eck3fya", length 7
            Assert.Equal("test::\u30a4\u30dd\u30e5", _demangler.Demangle("_RNvC4testu7eck3fya"));
        }

        #endregion

        #region Version Number After Prefix

        [Fact]
        public void WhenSymbolHasVersionNumberThenSkipsItSuccessfully()
        {
            // _R0NvC4test3foo — version 0
            Assert.Equal("test::foo", _demangler.Demangle("_R0NvC4test3foo"));
        }

        #endregion

        #region Trailing Data (Instantiating Crate)

        [Fact]
        public void WhenSymbolHasInstantiatingCrateSuffixThenParsesSuccessfully()
        {
            // Valid instantiating crate suffix: C3std (crate "std")
            Assert.Equal("test::foo::<&str>", _demangler.Demangle("_RINvC4test3fooReEC3std"));
        }

        [Theory]
        [InlineData("_RC3fooXYZ")]
        [InlineData("_RC3foo!!!")]
        [InlineData("_RNvC4test3foo$$$")]
        public void WhenSymbolHasTrailingGarbageThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Fact]
        public void WhenSymbolHasNoTrailingDataThenStillWorks()
        {
            Assert.Equal("foo", _demangler.Demangle("_RC3foo"));
        }

        #endregion

        #region Complex Nested Generics

        [Fact]
        public void WhenGenericHasDeeplyNestedTypesThenFormatsCorrectly()
        {
            // test::f::<a::V<b::O<c::B<dyn d::T>>>>
            // I NvC4test1f I NtC1a1V I NtC1b1O I NtC1c1B D NtC1d1T E L_ E E E E
            string result = _demangler.Demangle(
                "_RINvC4test1fINtC1a1VINtC1b1OINtC1c1BDNtC1d1TEL_EEEE");
            Assert.Equal("test::f::<a::V<b::O<c::B<dyn d::T>>>>", result);
        }

        [Fact]
        public void WhenGenericArgIsReferenceToStrThenFormatsCorrectly()
        {
            // Known test vector: test::foo::<&str>
            Assert.Equal("test::foo::<&str>", _demangler.Demangle("_RINvC4test3fooReE"));
        }

        [Fact]
        public void WhenGenericArgIsSliceOfReferenceThenFormatsCorrectly()
        {
            // test::f::<[&u8]>
            Assert.Equal("test::f::<[&u8]>", _demangler.Demangle("_RINvC4test1fSRhE"));
        }

        [Fact]
        public void WhenGenericArgIsTupleOfReferencesThenFormatsCorrectly()
        {
            // test::f::<(&i32, &mut u8)>
            Assert.Equal(
                "test::f::<(&i32, &mut u8)>",
                _demangler.Demangle("_RINvC4test1fTRlQhEE"));
        }

        [Fact]
        public void WhenGenericArgIsPointerToSliceThenFormatsCorrectly()
        {
            // test::f::<*const [u8]>
            Assert.Equal("test::f::<*const [u8]>", _demangler.Demangle("_RINvC4test1fPShE"));
        }

        [Fact]
        public void WhenMultipleGenericArgsWithMixedTypesThenFormatsCorrectly()
        {
            // test::f::<&str, i32, bool>
            Assert.Equal(
                "test::f::<&str, i32, bool>",
                _demangler.Demangle("_RINvC4test1fRelbE"));
        }

        #endregion

        #region Combined Trait Impl with Generics

        [Fact]
        public void WhenTraitImplHasGenericMethodArgsThenFormatsCorrectly()
        {
            // <i32 as test::MyTrait>::foo::<u8>
            // I (Nv (Xs_ C4test l NtC4test7MyTrait) 3foo) h E
            string result = _demangler.Demangle(
                "_RINvXs_C4testlNtC4test7MyTrait3foohE");
            Assert.Equal("<i32 as test::MyTrait>::foo::<u8>", result);
        }

        #endregion

        #region Vendor-Specific Suffix Stripping

        [Theory]
        [InlineData("_RNvC4test3foo.llvm.17209", "test::foo")]
        [InlineData("_RNvC4test3foo.llvm.123456789", "test::foo")]
        [InlineData("_RNvC4test3foo.anythinghere", "test::foo")]
        public void WhenSymbolHasVendorSuffixThenSuffixIsStrippedBeforeParsing(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Fact]
        public void WhenSymbolHasNoVendorSuffixThenStillParsesCorrectly()
        {
            Assert.Equal("test::foo", _demangler.Demangle("_RNvC4test3foo"));
        }

        [Fact]
        public void WhenSymbolHasVendorSuffixWithGenericsThenParsesCorrectly()
        {
            Assert.Equal("test::foo::<&str>", _demangler.Demangle("_RINvC4test3fooReE.llvm.99999"));
        }

        #endregion

        #region Comprehensive Theory: Known Demangle Pairs

        [Theory]
        [InlineData("_RC4test", "test")]
        [InlineData("_RNvC4test3foo", "test::foo")]
        [InlineData("_RNvNvC4test3foo3bar", "test::foo::bar")]
        [InlineData("_RINvC4test3fooReE", "test::foo::<&str>")]
        [InlineData("_RINvC4test3fooReEC3std", "test::foo::<&str>")]
        public void WhenInputIsKnownTestVectorThenDemangleMatchesExpected(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion

        #region Identifier With Separator Underscore

        [Fact]
        public void WhenIdentifierStartsWithDigitThenSeparatorIsConsumed()
        {
            // Identifier "1abc" → length=4, separator '_', bytes "1abc"
            // NvC4test4_1abc
            Assert.Equal("test::1abc", _demangler.Demangle("_RNvC4test4_1abc"));
        }

        [Fact]
        public void WhenIdentifierStartsWithUnderscoreThenSeparatorIsConsumed()
        {
            // Identifier "_foo" → length=4, separator '_', bytes "_foo"
            // NvC4test4__foo
            Assert.Equal("test::_foo", _demangler.Demangle("_RNvC4test4__foo"));
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void WhenCrateHasEmptyNameThenReturnsEmptyString()
        {
            // C + identifier with length 0
            Assert.Equal("", _demangler.Demangle("_RC0"));
        }

        [Fact]
        public void WhenNestedPathHasManyLevelsThenHandlesCorrectly()
        {
            // 6 levels deep
            Assert.Equal(
                "c::a::b::c::d::e::f",
                _demangler.Demangle("_RNvNvNvNvNvNvC1c1a1b1c1d1e1f"));
        }

        [Fact]
        public void WhenInputHasOnlyInvalidPathTagThenReturnsNull()
        {
            Assert.Null(_demangler.Demangle("_RW"));
        }

        [Fact]
        public void WhenInputIsTruncatedMidParseThenReturnsNull()
        {
            // Starts valid but ends prematurely
            Assert.Null(_demangler.Demangle("_RNvC4te"));
        }

        [Fact]
        public void WhenInputHasIncompleteGenericArgsThenReturnsNull()
        {
            // Missing the terminating E for generic args
            Assert.Null(_demangler.Demangle("_RINvC4test3fool"));
        }

        [Fact]
        public void WhenConstCharIsNullCharThenFormatsAsEscapedNull()
        {
            // char const U+0000 (null char) — hex empty → '\0'
            Assert.Equal("test::f::<'\\0'>", _demangler.Demangle("_RINvC4test1fKc_E"));
        }

        [Fact]
        public void WhenFnPointerHasCustomAbiThenShowsAbiName()
        {
            // extern "system" fn(i32) → FKC would be "C" abi,
            // but a non-C abi uses an undisambiguated identifier
            // extern "system" fn() — F K 6system E u
            Assert.Equal(
                "test::f::<extern \"system\" fn()>",
                _demangler.Demangle("_RINvC4test1fFK6systemEuE"));
        }

        [Fact]
        public void WhenDynHasHigherRankedLifetimeBinderThenParsesCorrectly()
        {
            // D G_ (binder count 0) ... E L_
            Assert.Equal(
                "test::f::<dyn test::Trait>",
                _demangler.Demangle("_RINvC4test1fDG_NtC4test5TraitEL_E"));
        }

        [Fact]
        public void WhenArrayConstTypeIsUsizeThenFormatsCorrectly()
        {
            // [bool; 5]
            Assert.Equal("test::f::<[bool; 5]>", _demangler.Demangle("_RINvC4test1fAbj5_E"));
        }

        [Fact]
        public void WhenNestedClosureThenFormatsCorrectly()
        {
            // test::foo::{closure}::{closure}
            Assert.Equal(
                "test::foo::{closure}::{closure}",
                _demangler.Demangle("_RNCNCNvC4test3foos_0s_0"));
        }

        [Fact]
        public void WhenMultipleGenericArgsIncludeConstAndTypeThenFormatsCorrectly()
        {
            // test::f::<i32, 5>
            Assert.Equal("test::f::<i32, 5>", _demangler.Demangle("_RINvC4test1flKl5_E"));
        }

        [Fact]
        public void WhenTypeIsReferenceToTupleThenFormatsCorrectly()
        {
            // &(i32, u8) → R T l h E
            Assert.Equal("test::f::<&(i32, u8)>", _demangler.Demangle("_RINvC4test1fRTlhEE"));
        }

        [Fact]
        public void WhenTypeIsMutPointerToFnPointerThenFormatsCorrectly()
        {
            // *mut fn(i32) → O F l E u
            Assert.Equal(
                "test::f::<*mut fn(i32)>",
                _demangler.Demangle("_RINvC4test1fOFlEuE"));
        }

        [Fact]
        public void WhenTraitImplMethodIsNestedThenFormatsCorrectly()
        {
            // <i32 as test::MyTrait>::inner::method
            string result = _demangler.Demangle(
                "_RNvNvXs_C4testlNtC4test7MyTrait5inner6method");
            Assert.Equal("<i32 as test::MyTrait>::inner::method", result);
        }

        [Fact]
        public void WhenClosureInsideTraitImplThenFormatsCorrectly()
        {
            // <i32 as test::MyTrait>::foo::{closure}
            string result = _demangler.Demangle(
                "_RNCNvXs_C4testlNtC4test7MyTrait3foos_0");
            Assert.Equal("<i32 as test::MyTrait>::foo::{closure}", result);
        }

        #endregion

        #region Overflow and Boundary Regression Tests

        // --- ParseDecimalNumber overflow (98af8265) ---

        [Theory]
        [InlineData("_RC99999999999999999999x")]        // 20-digit crate-name length overflows long
        [InlineData("_RNvC4test99999999999999999999x")]  // 20-digit nested identifier length overflows long
        public void WhenDecimalNumberOverflowsLongThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Fact]
        public void WhenIdentifierLengthExceedsInputButFitsInLongThenReturnsNull()
        {
            // 2,147,483,648 (int.MaxValue + 1) as identifier length: valid long but
            // far exceeds the actual input length, caught by the bounds check.
            Assert.Null(_demangler.Demangle("_RC2147483648x"));
        }

        // --- Base-62 overflow and dyn-bounds validation (3d7d9729) ---

        [Fact]
        public void WhenBase62NumberOverflowsLongThenDisambiguatorIsStillSkipped()
        {
            // Disambiguator "s" followed by 12 Z's (base-62 digit 61 each).
            // The numeric value overflows long, but unchecked arithmetic allows
            // the disambiguator to be consumed and discarded correctly.
            Assert.Equal("test", _demangler.Demangle("_RCsZZZZZZZZZZZZ_4test"));
        }

        [Fact]
        public void WhenDynBoundsInputTruncatedBeforeLifetimeThenReturnsNull()
        {
            // D NtC4test5Trait E — input ends immediately after the trait-list 'E',
            // leaving no 'L' lifetime to parse (the !HasMore branch of the guard).
            Assert.Null(_demangler.Demangle("_RINvC4test1fDNtC4test5TraitE"));
        }

        // --- Backref position validation before long-to-int cast (9daf4574) ---

        [Fact]
        public void WhenBackrefPositionExceedsInt32MaxThenReturnsNull()
        {
            // Base-62 "300000" → 3×62^5 = 2,748,398,496, +1 = 2,748,398,497 (> int.MaxValue).
            // Without the guard, the long-to-int cast would silently truncate.
            Assert.Null(_demangler.Demangle("_RINvC4test1fB300000_E"));
        }

        [Fact]
        public void WhenBackrefPositionExceedsInputLengthThenReturnsNull()
        {
            // Base-62 "Z" → 61, +1 = 62, which exceeds this ~22-char input.
            Assert.Null(_demangler.Demangle("_RINvC4test1fBZ_E"));
        }

        // --- AdaptBias delta parameter widened to long (1b95b83a) ---

        [Fact]
        public void WhenPunycodeDeltaExceedsInt32MaxThenAdaptBiasHandlesCorrectly()
        {
            // 8 max-value digits ('9' = Punycode digit 35) followed by 'a' (digit 0)
            // accumulate i to ~4.76×10^9, exceeding int.MaxValue (2,147,483,647).
            // AdaptBias receives this as a long parameter. Without the parameter
            // widening, the delta would be truncated to int before the call.
            // The decoded code point lands beyond U+10FFFF, so Punycode falls back
            // to the raw identifier name.
            Assert.Equal("test::99999999a", _demangler.Demangle("_RNvC4testu9_99999999a"));
        }

        // --- Checked arithmetic for Punycode w and i (eee7f8dd) ---

        [Fact]
        public void WhenPunycodeAccumulatorOverflowsLongThenDoesNotCorrupt()
        {
            // 18 max-value digits ('9' = digit 35) cause checked(i += digit * w)
            // to overflow long at the 18th inner-loop iteration. Without checked{}
            // the overflow would silently corrupt decode state. The decoder catches
            // OverflowException and falls back to the raw identifier.
            Assert.Equal("test::999999999999999999", _demangler.Demangle("_RNvC4testu18_999999999999999999"));
        }

        [Fact]
        public void WhenPunycodeManyHighDigitsThenOverflowIsHandledGracefully()
        {
            // 20 max-value digits — exercises the same checked arithmetic paths
            // through an even longer digit sequence to ensure robust overflow handling.
            Assert.Equal("test::99999999999999999999", _demangler.Demangle("_RNvC4testu20_99999999999999999999"));
        }

        #endregion

        #region Real-World Crate Symbol Regression Tests

        [Theory]
        [InlineData("_RNvCskmFptwX7bJS_2cc4fail", "cc::fail")]
        [InlineData("_RNvCshSfwqS0mSVt_4want3new", "want::new")]
        [InlineData("_RNvCskdGv34WPu6C_4glob4glob", "glob::glob")]
        public void WhenSymbolHasLargeDisambiguatorHashThenDemangleSucceeds(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_RINvCsaU8YLwTUn6A_4zmij9normalizemEB2_")]
        [InlineData("_RINvCsaU8YLwTUn6A_4zmij10to_decimalmEB2_")]
        public void WhenGenericArgIsU32ThenDemangleSucceeds(string input)
        {
            string result = _demangler.Demangle(input);
            Assert.NotNull(result);
        }

        [Theory]
        [InlineData("_RNCNCNvCskdGv34WPu6C_4glob9fill_todos_00B5_")]
        [InlineData("_RNCNCNCNvCskdGv34WPu6C_4glob9fill_todos_000B7_")]
        [InlineData("_RNCNCNvMs4_CskmFptwX7bJS_2ccNtB9_5Build5which00B9_")]
        public void WhenSymbolHasNestedClosuresWithZeroLengthIdentifiersThenDemangleSucceeds(string input)
        {
            string result = _demangler.Demangle(input);
            Assert.NotNull(result);
            Assert.Contains("{closure}", result);
        }

        [Theory]
        [InlineData("_RNvYFG_RL0_eEINtNtCsaz9ix1H7t6h_5alloc3vec3VecjENtNtCs5cYH0lLSdLc_4core5clone5Clone5cloneCsIOS3H4Mofs_8textwrap")]
        [InlineData("_RNvYFG_INtNtCs4EMmDgM0Qf0_3syn6buffer6CursorL0_EEbINtNtNtCs5cYH0lLSdLc_4core3ops8function6FnOnceTB6_EE9call_onceBa_")]
        public void WhenSymbolHasFnSigWithHigherRankedBinderThenDemangleSucceeds(string input)
        {
            string result = _demangler.Demangle(input);
            Assert.NotNull(result);
        }

        #endregion

        #region Benchmark Symbol Regression Tests

        [Theory]
        // Benchmark: Y impl - fn pointer type implementing trait
        [InlineData("_RNvYFEINtNtNtCs3NZLoJsOov_3std4sync5mutex5MutexNtNtCs97ToZhCJ7Fo_12thread_local9thread_id15ThreadIdManagerEINtNtNtCsiL7B6CFZHyD_4core3ops8function6FnOnceuE9call_onceBN_", "<fn() -> std::sync::mutex::Mutex<thread_local::thread_id::ThreadIdManager> as core::ops::function::FnOnce<()>>::call_once")]

        // Benchmark: Y impl - extern "C" fn pointer
        [InlineData("_RNvYFKCEuNtNtCsiL7B6CFZHyD_4core6marker5FnPtr4addrCsclAvUJFJRY0_4libc", "<extern \"C\" fn() as core::marker::FnPtr>::addr")]

        // Benchmark: Y impl - unsafe extern "C" fn pointer
        [InlineData("_RNvYFUKCNtCsi6pTmhHIU5M_9clang_sys6CXTypeEB6_NtNtCsiL7B6CFZHyD_4core6marker5FnPtr4addrB8_", "<unsafe extern \"C\" fn(clang_sys::CXType) -> clang_sys::CXType as core::marker::FnPtr>::addr")]

        // Benchmark: X trait impl - primitive types (char, f32)
        [InlineData("_RNvXCs5hFHXnc0CYL_13unicode_widthcNtB2_16UnicodeWidthChar5widthCsIOS3H4Mofs_8textwrap", "<char as unicode_width::UnicodeWidthChar>::width")]
        [InlineData("_RNvXCsaU8YLwTUn6A_4zmijfNtB2_11FloatTraits7to_bits", "<f32 as zmij::FloatTraits>::to_bits")]

        // Benchmark: X trait impl - generic types with backreferences
        [InlineData("_RNvXCsffYwmj09FSj_9hashbrownINtNtCsaz9ix1H7t6h_5alloc5boxed3BoxeEINtB2_10EquivalentBq_E10equivalentCskmFptwX7bJS_2cc", "<alloc::boxed::Box<str> as hashbrown::Equivalent<alloc::boxed::Box<str>>>::equivalent")]

        // Benchmark: Nested closures (2-3 levels)
        [InlineData("_RNCNCNvCskdGv34WPu6C_4glob9fill_todos_00B5_", "glob::fill_todo::{closure}::{closure}")]
        [InlineData("_RNCNCNCNvCskdGv34WPu6C_4glob9fill_todos_000B7_", "glob::fill_todo::{closure}::{closure}::{closure}")]

        // Benchmark: Deep nested closures (6 levels)
        [InlineData("_RINvMs5_NtCs6x17v5tx4u3_4pest12parser_stateINtB6_11ParserStateNtNtCs2mjHR4nEvfL_13semver_parser9generated4RuleE8optionalNCNCNCNCNCNCNvNtNtNvXs_B10_NtB12_12SemverParserINtNtB8_6parser6ParserBY_E5parse5rules7visible5range000s_000EB12_", "<pest::parser_state::ParserState<semver_parser::generated::Rule>>::optional::<<semver_parser::SemverParser as pest::parser::Parser<semver_parser::generated::Rule>>::parse::rules::visible::range::{closure}::{closure}::{closure}::{closure}::{closure}::{closure}>")]

        // Benchmark: Unsafe extern "C" function pointer types in generics
        [InlineData("_RINvCsi6pTmhHIU5M_9clang_sys12with_libraryFUKCNtB2_6CXTypeElNCNvB2_20clang_getNumArgTypes0EB2_", "clang_sys::with_library::<unsafe extern \"C\" fn(clang_sys::CXType) -> i32, clang_sys::clang_getNumArgTypes::{closure}>")]
        [InlineData("_RINvCsi6pTmhHIU5M_9clang_sys12with_libraryFUKCNtB2_8CXCursorENtB2_8CXStringNCNvB2_38clang_Cursor_getObjCPropertySetterName0EB2_", "clang_sys::with_library::<unsafe extern \"C\" fn(clang_sys::CXCursor) -> clang_sys::CXString, clang_sys::clang_Cursor_getObjCPropertySetterName::{closure}>")]

        // Benchmark: Shim vtable patterns (NS)
        [InlineData("_RNSNvYNCINvMNtCs3NZLoJsOov_3std6threadNtBa_7Builder16spawn_unchecked_NCNvCsbudUTgWQKts_10threadpool13spawn_in_pool0uEs_0INtNtNtCsiL7B6CFZHyD_4core3ops8function6FnOnceuE9call_once6vtableB19_", "<<std::thread::Builder>::spawn_unchecked_<threadpool::spawn_in_pool::{closure}, ()>::{closure} as core::ops::function::FnOnce<()>>::call_once::{shim:vtable}")]

        // Benchmark: Array types in generics [char; N]
        [InlineData("_RINvMNtCsb4rfMCCqXFm_10dissimilar5rangeNtB3_5Range11starts_withAcj2_EB5_", "<dissimilar::range::Range>::starts_with::<[char; 2]>")]
        [InlineData("_RINvMNtCsiL7B6CFZHyD_4core3stre8containsAcj2_ECskvdpLI7AOSZ_5insta", "<str>::contains::<[char; 2]>")]

        // Benchmark: Dyn trait with higher-ranked lifetime binder (DG0_)
        [InlineData("_RINvNtCs5cYH0lLSdLc_4core3ptr13drop_in_placeINtNtB4_6option6OptionINtNtCsaz9ix1H7t6h_5alloc5boxed3BoxDG0_INtNtNtB4_3ops8function5FnMutTRL1_NtNtCsa1Fkj6zmIPF_7walkdir4dent8DirEntryRL0_B2d_EEp6OutputNtNtB4_3cmp8OrderingNtNtB4_6marker4SendNtB3v_4SyncEL_EEEB2h_", "core::ptr::drop_in_place::<core::option::Option<alloc::boxed::Box<dyn core::ops::function::FnMut<(&walkdir::dent::DirEntry, &walkdir::dent::DirEntry)><Output = core::cmp::Ordering> + core::marker::Send + core::marker::Sync>>>")]

        // Benchmark: Multiple backreferences in complex generic context
        [InlineData("_RINvMCslmJ0Mh1KeWG_10pkg_configNtB3_14WrappedCommand4argsRINtNtCsaz9ix1H7t6h_5alloc3vec3VecNtNtNtCs2FIVCi1Wrow_3std3ffi6os_str8OsStringERB1r_EB3_", "<pkg_config::WrappedCommand>::args::<&alloc::vec::Vec<std::ffi::os_str::OsString>, &std::ffi::os_str::OsString>")]

        // Benchmark: Impl with Range generic parameter
        [InlineData("_RINvMNtCs25ZhrkksEen_5bytes5bytesNtB3_5Bytes5sliceINtNtNtCsiL7B6CFZHyD_4core3ops5range5RangejEEB5_", "<bytes::bytes::Bytes>::slice::<core::ops::range::Range<usize>>")]

        // Benchmark: Complex trait impl with nested closures and generics
        [InlineData("_RNvXNCNvNtNtNtCs4USifnV5z1l_5tokio7runtime4task7harness11poll_future0INtB2_5GuardINtNtNtBa_8blocking4task12BlockingTaskNCNvXNtNtBc_2io8blockingINtB1Y_8BlockingNtNtNtCs3NZLoJsOov_3std2io5stdio5StdinENtNtB20_10async_read9AsyncRead9poll_read0ENtNtB1m_8schedule16BlockingScheduleENtNtNtCsiL7B6CFZHyD_4core3ops4drop4Drop4dropBc_", "<tokio::runtime::task::harness::poll_future::{closure}::Guard<tokio::runtime::blocking::task::BlockingTask<<tokio::io::blocking::Blocking<std::io::stdio::Stdin> as tokio::io::async_read::AsyncRead>::poll_read::{closure}>, tokio::runtime::blocking::schedule::BlockingSchedule> as core::ops::drop::Drop>::drop")]
        public void WhenBenchmarkSymbolIsDemangedThenMatchesExpected(string input, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(input));
        }

        #endregion
    }
}
