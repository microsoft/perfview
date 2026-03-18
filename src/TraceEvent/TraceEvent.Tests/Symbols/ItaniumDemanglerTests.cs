using Microsoft.Diagnostics.Symbols;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class ItaniumDemanglerTests : TestBase
    {
        private readonly ItaniumDemangler _demangler = new ItaniumDemangler();
        public ItaniumDemanglerTests(ITestOutputHelper output)
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
        [InlineData("_Y3foo")]
        [InlineData("_")]
        [InlineData("_Z")]
        [InlineData("main")]
        [InlineData("printf")]
        [InlineData("__libc_start_main")]
        public void WhenInputIsNotMangledThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Theory]
        [InlineData("_Z!")]
        [InlineData("_Z$$")]
        [InlineData("_Za")]
        public void WhenInputIsMalformedMangledNameThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        #endregion

        #region Simple Functions

        [Theory]
        [InlineData("_Z3foov", "foo()")]
        [InlineData("_Z3bari", "bar(int)")]
        [InlineData("_Z4funcf", "func(float)")]
        [InlineData("_Z4funcd", "func(double)")]
        [InlineData("_Z4funcb", "func(bool)")]
        [InlineData("_Z4funcc", "func(char)")]
        [InlineData("_Z4funcl", "func(long)")]
        [InlineData("_Z4funcx", "func(long long)")]
        [InlineData("_Z4funcw", "func(wchar_t)")]
        public void SimpleFunction(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Multiple Parameters

        [Theory]
        [InlineData("_Z3bazlPci", "baz(long, char*, int)")]
        [InlineData("_Z4funcid", "func(int, double)")]
        [InlineData("_Z4funcidf", "func(int, double, float)")]
        [InlineData("_Z4funcidfl", "func(int, double, float, long)")]
        [InlineData("_Z4funcidPcRi", "func(int, double, char*, int&)")]
        public void MultiplParameters(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Nested Names

        [Theory]
        [InlineData("_ZN3foo3barEv", "foo::bar()")]
        [InlineData("_ZN3foo3bar3bazEi", "foo::bar::baz(int)")]
        [InlineData("_ZN1a1b1c1d1eEv", "a::b::c::d::e()")]
        [InlineData("_ZN5Outer5Inner4funcEi", "Outer::Inner::func(int)")]
        public void NestedName(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Constructors and Destructors

        [Theory]
        [InlineData("_ZN3FooC1Ev", "Foo::Foo()")]
        [InlineData("_ZN3FooC2Ev", "Foo::Foo()")]
        [InlineData("_ZN3FooC3Ev", "Foo::Foo()")]
        [InlineData("_ZN3FooC2Ei", "Foo::Foo(int)")]
        [InlineData("_ZN3FooC1Eid", "Foo::Foo(int, double)")]
        [InlineData("_ZN3FooD0Ev", "Foo::~Foo()")]
        [InlineData("_ZN3FooD1Ev", "Foo::~Foo()")]
        [InlineData("_ZN3FooD2Ev", "Foo::~Foo()")]
        [InlineData("_ZN5Outer5InnerC1Ev", "Outer::Inner::Inner()")]
        [InlineData("_ZN5Outer5InnerD1Ev", "Outer::Inner::~Inner()")]
        public void ConstructorAndDestructor(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Operator Overloads

        [Theory]
        // Arithmetic operators
        [InlineData("_ZN3FooplERKS_", "Foo::operator+(Foo const&)")]
        [InlineData("_ZN3FoomiERKS_", "Foo::operator-(Foo const&)")]
        [InlineData("_ZN3FoomlERKS_", "Foo::operator*(Foo const&)")]
        [InlineData("_ZN3FoodvERKS_", "Foo::operator/(Foo const&)")]
        [InlineData("_ZN3FoormERKS_", "Foo::operator%(Foo const&)")]
        // Comparison operators
        [InlineData("_ZN3FooeqERKS_", "Foo::operator==(Foo const&)")]
        [InlineData("_ZN3FooneERKS_", "Foo::operator!=(Foo const&)")]
        [InlineData("_ZN3FooltERKS_", "Foo::operator<(Foo const&)")]
        [InlineData("_ZN3FoogtERKS_", "Foo::operator>(Foo const&)")]
        [InlineData("_ZN3FooleERKS_", "Foo::operator<=(Foo const&)")]
        [InlineData("_ZN3FoogeERKS_", "Foo::operator>=(Foo const&)")]
        [InlineData("_ZN3FoossERKS_", "Foo::operator<=>(Foo const&)")]
        // Bitwise operators
        [InlineData("_ZN3FooanERKS_", "Foo::operator&(Foo const&)")]
        [InlineData("_ZN3FooorERKS_", "Foo::operator|(Foo const&)")]
        [InlineData("_ZN3FooeoERKS_", "Foo::operator^(Foo const&)")]
        [InlineData("_ZN3FoolsERKS_", "Foo::operator<<(Foo const&)")]
        [InlineData("_ZN3FoorsERKS_", "Foo::operator>>(Foo const&)")]
        // Assignment operators
        [InlineData("_ZN3FooaSERKS_", "Foo::operator=(Foo const&)")]
        [InlineData("_ZN3FoopLERKS_", "Foo::operator+=(Foo const&)")]
        [InlineData("_ZN3FoomIERKS_", "Foo::operator-=(Foo const&)")]
        [InlineData("_ZN3FoomLERKS_", "Foo::operator*=(Foo const&)")]
        [InlineData("_ZN3FoodVERKS_", "Foo::operator/=(Foo const&)")]
        // Call/index operators
        [InlineData("_ZN3FooclEv", "Foo::operator()()")]
        [InlineData("_ZN3FooclEi", "Foo::operator()(int)")]
        [InlineData("_ZN3FooixEi", "Foo::operator[](int)")]
        // Increment/decrement
        [InlineData("_ZN3FooppEv", "Foo::operator++()")]
        [InlineData("_ZN3FoommEv", "Foo::operator--()")]
        // Logical operators
        [InlineData("_ZN3FoontEv", "Foo::operator!()")]
        [InlineData("_ZN3FooaaERKS_", "Foo::operator&&(Foo const&)")]
        [InlineData("_ZN3FooooERKS_", "Foo::operator||(Foo const&)")]
        // Special operators
        [InlineData("_ZN3FooptEv", "Foo::operator->()")]
        [InlineData("_ZN3FoopmERKS_", "Foo::operator->*(Foo const&)")]
        [InlineData("_ZN3FoocoEv", "Foo::operator~()")]
        [InlineData("_ZN3FoocmERKS_", "Foo::operator,(Foo const&)")]
        public void OperatorOverload(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Global new/delete
        [InlineData("_Znwm", "operator new(unsigned long)")]
        [InlineData("_Znaj", "operator new[](unsigned int)")]
        [InlineData("_ZdlPv", "operator delete(void*)")]
        [InlineData("_ZdaPv", "operator delete[](void*)")]
        // Global operator+
        [InlineData("_Zplii", "operator+(int, int)")]
        public void GlobalOperator(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Fact]
        public void CastOperator()
        {
            // Foo::operator Bar()
            Assert.Equal("Foo::operator Bar()", _demangler.Demangle("_ZN3Foocv3BarEv"));
        }

        #endregion

        #region Templates

        [Theory]
        // Simple template function (return type is discarded for template functions)
        [InlineData("_Z1fIiEvi", "f<int>(int)")]
        [InlineData("_Z1fIdEvd", "f<double>(double)")]
        // Template with template param reference (T_)
        [InlineData("_Z1fIiEvT_", "f<int>(int)")]
        // Multiple template args with T_ and T0_
        [InlineData("_Z1fIidEvT_T0_", "f<int, double>(int, double)")]
        // Template with nested class arg
        [InlineData("_Z1fIN3Foo3BarEEvi", "f<Foo::Bar>(int)")]
        // Template with template class arg
        [InlineData("_Z1fI1AIiEEvi", "f<A<int>>(int)")]
        // Nested name with template
        [InlineData("_ZN3Foo3barIiEEvi", "Foo::bar<int>(int)")]
        // Member template using substitution
        [InlineData("_ZN3Foo3barIS_EEvT_", "Foo::bar<Foo>(Foo)")]
        // Multiple template args
        [InlineData("_Z1fIifEvi", "f<int, float>(int)")]
        public void Template(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Template Arguments (Non-Type)

        [Theory]
        // Integer literal
        [InlineData("_Z1fILi42EEvi", "f<(int)42>(int)")]
        // Negative integer literal
        [InlineData("_Z1fILin1EEvi", "f<(int)-1>(int)")]
        // Boolean true literal
        [InlineData("_Z1fILb1EEvi", "f<true>(int)")]
        // Boolean false literal
        [InlineData("_Z1fILb0EEvi", "f<false>(int)")]
        // Zero integer literal
        [InlineData("_Z1fILi0EEvi", "f<(int)0>(int)")]
        public void NonTypeTemplateArg(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Fact]
        public void TemplateArgPack()
        {
            // J i d E = argument pack with int, double
            Assert.Equal("f<int, double>()", _demangler.Demangle("_Z1fIJidEEvv"));
        }

        #endregion

        #region CV-Qualifiers on Types

        [Theory]
        [InlineData("_Z1fKi", "f(int const)")]
        [InlineData("_Z1fVi", "f(int volatile)")]
        [InlineData("_Z1fri", "f(int restrict)")]
        [InlineData("_Z1fKVi", "f(int volatile const)")]
        [InlineData("_Z1fVKi", "f(int volatile const)")]
        [InlineData("_Z1fKVri", "f(int restrict volatile const)")]
        public void CVQualifiedType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Truncated CV-qualified type: K/V/r with no inner type should return null.
        // _pos is restored so the trailing qualifier is unconsumed, making IsAtEnd false.
        [InlineData("_Z1fK")]
        [InlineData("_Z1fV")]
        [InlineData("_Z1fKV")]
        [InlineData("_Z3fooK")]
        [InlineData("_Z3fooKV")]
        public void WhenCVQualifiedTypeIsTruncatedThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Fact]
        public void WhenCVQualifiedTypeHasValidInnerTypeThenSucceeds()
        {
            // K + int = "int const"
            Assert.Equal("foo(int const)", _demangler.Demangle("_Z3fooKi"));
        }

        #endregion

        #region Pointer and Reference Types

        [Theory]
        [InlineData("_Z1fPi", "f(int*)")]
        [InlineData("_Z1fPc", "f(char*)")]
        [InlineData("_Z1fRi", "f(int&)")]
        [InlineData("_Z1fOi", "f(int&&)")]
        [InlineData("_Z1fPPi", "f(int**)")]
        [InlineData("_Z1fRPi", "f(int*&)")]
        // Const reference
        [InlineData("_Z1fRKi", "f(int const&)")]
        // Pointer to const
        [InlineData("_Z1fPKi", "f(int const*)")]
        // Const pointer to int
        [InlineData("_Z1fKPi", "f(int* const)")]
        // Const pointer to const
        [InlineData("_Z1fKPKi", "f(int const* const)")]
        // Rvalue reference to const
        [InlineData("_Z1fOKi", "f(int const&&)")]
        public void PointerAndReferenceType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Function Pointer Types

        [Theory]
        // void (*)(int) — function pointer as parameter
        [InlineData("_Z1fPFviE", "f(void (*)(int))")]
        // int (*)(double) — function pointer returning int
        [InlineData("_Z1fPFidE", "f(int (*)(double))")]
        // void (*)() — function pointer with no params
        [InlineData("_Z1fPFvvE", "f(void (*)())")]
        // Function reference
        [InlineData("_Z1fRFviE", "f(void (&)(int))")]
        // Function pointer with multiple params
        [InlineData("_Z1fPFvidE", "f(void (*)(int, double))")]
        // Nested function pointer: pointer to function taking function pointer
        [InlineData("_Z1fPFvPFiiEE", "f(void (*)(int (*)(int)))")]
        // Pointer to array
        [InlineData("_Z1fPA10_i", "f(int (*)[10])")]
        // Reference to array
        [InlineData("_Z1fRA10_i", "f(int (&)[10])")]
        // Rvalue reference to function
        [InlineData("_Z1fOFviE", "f(void (&&)(int))")]
        public void FunctionPointerType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Array Types

        [Theory]
        [InlineData("_Z1fA10_i", "f(int[10])")]
        [InlineData("_Z1fA5_c", "f(char[5])")]
        // Array of unknown bound
        [InlineData("_Z1fA_i", "f(int[])")]
        // Array of pointers
        [InlineData("_Z1fA3_Pi", "f(int*[3])")]
        // Multi-dimensional arrays: dimensions must appear in parse order (outer first)
        [InlineData("_Z1fA10_A5_i", "f(int[10][5])")]
        [InlineData("_Z1fA3_A4_A5_i", "f(int[3][4][5])")]
        // Array of function pointers: dimension goes on the pointer, not inside function params
        [InlineData("_Z1fA3_PFvA5_iE", "f(void (*)(int[5])[3])")]
        public void ArrayType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region All Builtin Types

        [Theory]
        [InlineData("_Z1fv", "f()")]              // void (single void → empty params)
        [InlineData("_Z1fw", "f(wchar_t)")]
        [InlineData("_Z1fb", "f(bool)")]
        [InlineData("_Z1fc", "f(char)")]
        [InlineData("_Z1fa", "f(signed char)")]
        [InlineData("_Z1fh", "f(unsigned char)")]
        [InlineData("_Z1fs", "f(short)")]
        [InlineData("_Z1ft", "f(unsigned short)")]
        [InlineData("_Z1fi", "f(int)")]
        [InlineData("_Z1fj", "f(unsigned int)")]
        [InlineData("_Z1fl", "f(long)")]
        [InlineData("_Z1fm", "f(unsigned long)")]
        [InlineData("_Z1fx", "f(long long)")]
        [InlineData("_Z1fy", "f(unsigned long long)")]
        [InlineData("_Z1fn", "f(__int128)")]
        [InlineData("_Z1fo", "f(unsigned __int128)")]
        [InlineData("_Z1ff", "f(float)")]
        [InlineData("_Z1fd", "f(double)")]
        [InlineData("_Z1fe", "f(long double)")]
        [InlineData("_Z1fg", "f(__float128)")]
        [InlineData("_Z1fz", "f(...)")]
        public void BuiltinType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        [InlineData("_Z1fDd", "f(decimal64)")]
        [InlineData("_Z1fDe", "f(decimal128)")]
        [InlineData("_Z1fDf", "f(decimal32)")]
        [InlineData("_Z1fDh", "f(half)")]
        [InlineData("_Z1fDi", "f(char32_t)")]
        [InlineData("_Z1fDs", "f(char16_t)")]
        [InlineData("_Z1fDa", "f(auto)")]
        [InlineData("_Z1fDc", "f(decltype(auto))")]
        [InlineData("_Z1fDn", "f(std::nullptr_t)")]
        public void DPrefixedBuiltinType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Special Names

        [Theory]
        [InlineData("_ZTV3Foo", "vtable for Foo")]
        [InlineData("_ZTI3Foo", "typeinfo for Foo")]
        [InlineData("_ZTS3Foo", "typeinfo name for Foo")]
        [InlineData("_ZTT3Foo", "VTT for Foo")]
        [InlineData("_ZTV5Outer", "vtable for Outer")]
        // Nested class vtable
        [InlineData("_ZTVN3Foo3BarE", "vtable for Foo::Bar")]
        // Guard variable
        [InlineData("_ZGV1x", "guard variable for x")]
        [InlineData("_ZGV5mutex", "guard variable for mutex")]
        // Reference temporary: GR <name> [<seq-id>] _
        [InlineData("_ZGR1x", "reference temporary for x")]
        [InlineData("_ZGR3foo_", "reference temporary for foo")]
        [InlineData("_ZGR3foo0_", "reference temporary for foo")]
        [InlineData("_ZGR3foo1_", "reference temporary for foo")]
        public void SpecialName(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Thunks

        [Fact]
        public void CovariantReturnThunk()
        {
            // Tc h0_ h0_ N3Foo3barEv
            Assert.Equal(
                "covariant return thunk to Foo::bar()",
                _demangler.Demangle("_ZTch0_h0_N3Foo3barEv"));
        }

        [Theory]
        // Non-virtual thunk with positive offset
        [InlineData("_ZThn8_N3Foo3barEv", "thunk to Foo::bar()")]
        // Non-virtual thunk with negative offset
        [InlineData("_ZThn16_N3Foo3barEv", "thunk to Foo::bar()")]
        // Virtual thunk
        [InlineData("_ZTv0_n24_N3Foo3barEv", "thunk to Foo::bar()")]
        public void NonVirtualAndVirtualThunk(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenThunkOffsetExceedsIntMaxThenDemangleSucceeds()
        {
            // Offset 2147483648 exceeds int.MaxValue; ParseSignedNumber must use long.
            Assert.NotNull(_demangler.Demangle("_ZTh2147483648_N1C1fEv"));
        }

        #endregion

        #region Substitutions (Well-Known)

        [Theory]
        [InlineData("_Z1fSa", "f(std::allocator)")]
        [InlineData("_Z1fSb", "f(std::basic_string)")]
        [InlineData("_Z1fSs", "f(std::basic_string<char, std::char_traits<char>, std::allocator<char>>)")]
        [InlineData("_Z1fSi", "f(std::basic_istream<char, std::char_traits<char>>)")]
        [InlineData("_Z1fSo", "f(std::basic_ostream<char, std::char_traits<char>>)")]
        [InlineData("_Z1fSd", "f(std::basic_iostream<char, std::char_traits<char>>)")]
        public void WellKnownSubstitution(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Substitutions (Numbered)

        [Theory]
        // S_ references first substitution (index 0)
        [InlineData("_ZN3Foo3barES_S_", "Foo::bar(Foo, Foo)")]
        // S0_ references second substitution (index 1)
        [InlineData("_ZN3Foo3barES_S0_", "Foo::bar(Foo, Foo::bar)")]
        public void NumberedSubstitution(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Local Names

        [Theory]
        [InlineData("_ZZ3foovE3bar", "foo()::bar")]
        [InlineData("_ZZ4mainvE5local", "main()::local")]
        // String literal
        [InlineData("_ZZ3foovEs", "foo()::string literal")]
        // Local name with single-underscore discriminator (second occurrence of bar in foo)
        [InlineData("_ZZ3foovE3bar_0", "foo()::bar")]
        // Local name with double-underscore discriminator (value >= 10)
        [InlineData("_ZZ3foovE3bar__15_", "foo()::bar")]
        // String literal with double-underscore discriminator
        [InlineData("_ZZ3foovEs__3_", "foo()::string literal")]
        public void LocalName(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Local Names With Template Args

        [Theory]
        // Non-template local entity inside template function — the inner ParseEncoding
        // must not leak _encodingHasTemplateArgs to the outer encoding.
        [InlineData("_ZZ3fooIiEvE3bari", "foo<int>()::bar(int)")]
        // Template local entity inside template function — both have template args.
        // bar<int> is a template so the ABI encodes a return type; 'i' is the return type (int).
        [InlineData("_ZZ3fooIiEvE3barIiEi", "foo<int>()::bar<int>()")]
        // Non-template function, non-template local entity — baseline.
        [InlineData("_ZZ3foovE3bari", "foo()::bar(int)")]
        // Local entity inside const member function — the inner ParseEncoding must not
        // leak _functionQualifiers (" const") to the outer encoding's entity.
        [InlineData("_ZZNK3Foo3barEvE3bazi", "Foo::bar() const::baz(int)")]
        public void LocalNameWithTemplateArgs(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Unnamed Types

        [Theory]
        // First unnamed type (Ut_)
        [InlineData("_ZN1AUt_Ev", "A::{unnamed type#1}()")]
        // Second unnamed type (Ut0_)
        [InlineData("_ZN1AUt0_Ev", "A::{unnamed type#2}()")]
        // Third unnamed type (Ut1_)
        [InlineData("_ZN1AUt1_Ev", "A::{unnamed type#3}()")]
        public void UnnamedType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Const/Volatile Member Functions

        [Theory]
        [InlineData("_ZNK3Foo3getEv", "Foo::get() const")]
        [InlineData("_ZNV3Foo3getEv", "Foo::get() volatile")]
        [InlineData("_ZNKVr3Foo3getEv", "Foo::get() const volatile restrict")]
        [InlineData("_ZNK3Foo3getEi", "Foo::get(int) const")]
        // const preserved when a parameter type triggers nested-name parsing
        [InlineData("_ZNK3Foo3barEN3Baz4quuxE", "Foo::bar(Baz::quux) const")]
        public void CVQualifiedMemberFunction(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Ref-Qualified Member Functions

        [Theory]
        // & (lvalue ref-qualified)
        [InlineData("_ZNR3Foo3getEv", "Foo::get() &")]
        // && (rvalue ref-qualified)
        [InlineData("_ZNO3Foo3getEv", "Foo::get() &&")]
        // const &
        [InlineData("_ZNKR3Foo3getEv", "Foo::get() const &")]
        // const &&
        [InlineData("_ZNKO3Foo3getEv", "Foo::get() const &&")]
        public void RefQualifiedMemberFunction(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Expressions in Template Args

        [Theory]
        // sizeof(type)
        [InlineData("_Z1fIXstiEEvi", "f<sizeof(int)>(int)")]
        // Unary negate expression: ng T_ inside template arg X...E
        // T_ resolves to a placeholder since the template args being parsed are not yet active.
        [InlineData("_Z1fIiXngT_EEvi", "f<int, -(T)>(int)")]
        public void ExpressionTemplateArg(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Vendor Extended Type

        [Fact]
        public void VendorExtendedType()
        {
            Assert.Equal("f(MyType)", _demangler.Demangle("_Z1fu6MyType"));
        }

        #endregion

        #region std:: Prefix (St)

        [Theory]
        // Unscoped name with std:: prefix
        [InlineData("_ZSt3absi", "std::abs(int)")]
        [InlineData("_ZSt4sortPiS_", "std::sort(int*, int*)")]
        public void StdPrefixedName(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region C99 Complex and Imaginary

        [Theory]
        [InlineData("_Z1fCd", "f(double _Complex)")]
        [InlineData("_Z1fGd", "f(double _Imaginary)")]
        [InlineData("_Z1fCf", "f(float _Complex)")]
        public void ComplexAndImaginaryType(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Pointer-to-Member

        [Fact]
        public void PointerToMember()
        {
            // M <class> <member-type>: M 1A i → int A::*
            Assert.Equal("f(int A::*)", _demangler.Demangle("_Z1fM1Ai"));
        }

        [Theory]
        // Pointer to member function: int (C::*)()
        [InlineData("_Z1fM1CFivE", "f(int (C::*)())")]
        // Pointer to const member function: int (C::*)() const
        [InlineData("_Z1fM1CKFivE", "f(int (C::*)() const)")]
        // Pointer to lvalue-ref-qualified member function: int (C::*)() &
        [InlineData("_Z1fM1CFivRE", "f(int (C::*)() &)")]
        // Pointer to rvalue-ref-qualified member function: int (C::*)() &&
        [InlineData("_Z1fM1CFivOE", "f(int (C::*)() &&)")]
        public void PointerToMemberFunction(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Pointer to noexcept member function: void (C::*)() noexcept
        [InlineData("_Z1fM1CFvDoE", "f(void (C::*)() noexcept)")]
        // Pointer to noexcept member function with ref-qualifier: void (C::*)() noexcept &
        [InlineData("_Z1fM1CFvDoRE", "f(void (C::*)() noexcept &)")]
        // Pointer to member function with throw spec: void (C::*)() throw(int)
        [InlineData("_Z1fM1CFvDwiEE", "f(void (C::*)() throw(int))")]
        public void PointerToMemberFunctionWithExceptionSpec(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Ellipsis (Variadic)

        [Theory]
        [InlineData("_Z1fiz", "f(int, ...)")]
        [InlineData("_Z6printfPKcz", "printf(char const*, ...)")]
        public void VariadicFunction(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Exception Specifications in Function Types

        [Theory]
        // bar(void (*)(int) noexcept): function taking a pointer to noexcept function
        [InlineData("_Z3barPFviDoE", "bar(void (*)(int) noexcept)")]
        // A::foo(void (*)() noexcept): member function taking noexcept function pointer
        [InlineData("_ZN1A3fooEPFvDoE", "A::foo(void (*)() noexcept)")]
        public void WhenFunctionTypeHasNoexceptThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Function type with throw(int): Dw i E
        [InlineData("_Z3barPFviDwiEE", "bar(void (*)(int) throw(int))")]
        public void WhenFunctionTypeHasThrowSpecThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Function type with noexcept(expr): DO <expression> E
        [InlineData("_Z3barPFviDOLb1EEE", "bar(void (*)(int) noexcept(true))")]
        public void WhenFunctionTypeHasNoexceptExprThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Pointer to noexcept function
        [InlineData("_Z1fPFidDoE", "f(int (*)(double) noexcept)")]
        // Reference to noexcept function
        [InlineData("_Z1fRFidDoE", "f(int (&)(double) noexcept)")]
        // Pointer to ref-qualified function
        [InlineData("_Z1fPFidRE", "f(int (*)(double) &)")]
        // Pointer to noexcept ref-qualified function
        [InlineData("_Z1fPFidDoRE", "f(int (*)(double) noexcept &)")]
        // Regression: plain function pointer still works
        [InlineData("_Z1fPFidE", "f(int (*)(double))")]
        public void WhenPointerOrRefToFunctionWithExceptionSpecThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Truncated after D — not enough data for exception spec
        [InlineData("_Z3barPFviD")]
        public void WhenExceptionSpecIsTruncatedThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        #endregion

        #region Expression Parsing

        [Theory]
        // cv operator (type conversion): (int)((int)5) in template argument
        [InlineData("_Z1fIXcviLi5EEEiv", "f<(int)((int)5)>()")]
        public void WhenExpressionHasCvCastThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // cv operator with list-initialization: (int){(int)1, (int)2}
        [InlineData("_Z1fIXcvi_Li1ELi2EEEEiv", "f<(int){(int)1, (int)2}>()")]
        public void WhenExpressionHasCvListInitThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenExprPrimaryReferencesEncodingThenFunctionTemplateArgsPreserved()
        {
            // L _Z <encoding> E inside a template argument must not clobber the
            // outer function's _functionTemplateArgs. Template args: <int, bar()>,
            // return type void, parameter T_ (resolves to int from the outer template).
            string result = _demangler.Demangle("_Z1fIiL_Z3barvEEvT_");
            Assert.NotNull(result);
            // The result should contain both "bar()" from the L _Z ref and "int"
            // from T_ resolving to the outer template arg.
            Assert.Contains("bar()", result);
            Assert.Contains("int", result);
        }

        [Theory]
        // static_cast<int>((int)5) as template expression argument
        [InlineData("_Z1fIXsciLi5EEEiv", "f<static_cast<int>((int)5)>()")]
        // reinterpret_cast<int>((int)5)
        [InlineData("_Z1fIXrciLi5EEEiv", "f<reinterpret_cast<int>((int)5)>()")]
        // dynamic_cast<int>((int)5)
        [InlineData("_Z1fIXdciLi5EEEiv", "f<dynamic_cast<int>((int)5)>()")]
        // const_cast<int>((int)5)
        [InlineData("_Z1fIXcciLi5EEEiv", "f<const_cast<int>((int)5)>()")]
        public void WhenExpressionHasNamedCastThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenExpressionHasMemberAccessThenDemangleSucceeds()
        {
            // dt: member access — expr.name. dt Li5E 3foo = ((int)5).foo
            string result = _demangler.Demangle("_Z1fIXdtLi5E3fooEEiv");
            Assert.NotNull(result);
            Assert.Contains(".foo", result);
        }

        [Fact]
        public void WhenExpressionHasPointerToMemberDerefThenDemangleSucceeds()
        {
            // ds: pointer-to-member dereference — lhs.*rhs
            string result = _demangler.Demangle("_Z1fIXdsLi5ELi3EEEiv");
            Assert.NotNull(result);
            Assert.Contains(".*", result);
        }

        [Theory]
        // sr 1A 3foo = A::foo (simple scope resolution)
        [InlineData("_Z1fIXsr1A3fooEEiv", "f<A::foo>()")]
        // sr 1A 3foo IiE = A::foo<int> (scope resolution with template args on name)
        [InlineData("_Z1fIXsr1A3fooIiEEEiv", "f<A::foo<int>>()")]
        public void WhenExpressionHasScopeResolutionThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenScopeResolutionHasNestedTypeThenDemangleSucceeds()
        {
            // sr with a nested-name type: sr N 1A 1B E 3foo = A::B::foo
            string result = _demangler.Demangle("_Z1fIXsrN1A1BE3fooEEvi");
            Assert.NotNull(result);
            Assert.Contains("A::B::foo", result);
        }

        #endregion

        #region Complex Real-World Names

        [Fact]
        public void StdVectorSize()
        {
            // std::vector<int, std::allocator<int>>::size()
            Assert.Equal(
                "std::vector<int, std::allocator<int>>::size()",
                _demangler.Demangle("_ZNK3std6vectorIiNS_9allocatorIiEEE4sizeEv"));
        }

        [Fact]
        public void NestedTemplateFunction()
        {
            // f<A<int>, B<double>>(A<int>, B<double>)
            Assert.Equal(
                "f<A<int>, B<double>>(A<int>, B<double>)",
                _demangler.Demangle("_Z1fI1AIiE1BIdEEvT_T0_"));
        }

        [Fact]
        public void DataName()
        {
            // Variable (data name) — no parameters
            string result = _demangler.Demangle("_ZN3Foo5valueE");
            Assert.Equal("Foo::value", result);
        }

        [Fact]
        public void GuardVariableForNestedName()
        {
            Assert.Equal(
                "guard variable for Foo::instance",
                _demangler.Demangle("_ZGVN3Foo8instanceE"));
        }

        [Fact]
        public void TypeinfoForNestedClass()
        {
            Assert.Equal(
                "typeinfo for Foo::Bar",
                _demangler.Demangle("_ZTIN3Foo3BarE"));
        }

        #endregion

        #region Member Template Parameter Resolution

        [Theory]
        // A<int>::foo<unsigned int>(T_) — T_ should resolve to unsigned int (foo's arg), not int (A's arg)
        [InlineData("_ZN1AIiE3fooIjEEvT_", "A<int>::foo<unsigned int>(unsigned int)")]
        // A<int>::bar<double>(T_) — T_ should resolve to double
        [InlineData("_ZN1AIiE3barIdEEvT_", "A<int>::bar<double>(double)")]
        public void MemberTemplateParamResolution(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Template param in parameter type that is itself a template specialization.
        // T_ should resolve to int (foo's template arg), not float (A's template arg).
        [InlineData("_Z3fooIiEv1AIfET_", "foo<int>(A<float>, int)")]
        public void WhenParamTypeIsTemplateSpecializationThenTResolvesFromFunctionArgs(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // foo<int>(int, A<int>, T0) — T0_ exceeds _functionTemplateArgs (1 entry), so it
        // must NOT fall back to the stale _templateArguments (which A<int> may have mutated).
        [InlineData("_Z3fooIiEvT_1AIiET0_", "foo<int>(int, A<int>, T0)")]
        public void WhenHighTemplateIndexExceedsFunctionArgsThenPlaceholderIsUsed(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Input Consumption

        [Theory]
        // Trailing garbage after valid encoding should be rejected
        [InlineData("_Z3foovXYZ")]
        [InlineData("_Z3foov!")]
        [InlineData("_Z3foov123")]
        public void WhenTrailingGarbageThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        [Theory]
        // Valid encodings with no trailing data should still work
        [InlineData("_Z3foov", "foo()")]
        [InlineData("_Z3bari", "bar(int)")]
        public void WhenNoTrailingGarbageThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Recursion Depth Limit

        [Fact]
        public void WhenDeeplyNestedPointerTypeThenReturnsNull()
        {
            // 300+ P prefixes followed by 'i' — should hit the depth limit and return null.
            string mangled = "_Z1f" + new string('P', 300) + "i";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenDeeplyNestedArgPackThenReturnsNull()
        {
            // 300+ nested J...E argument packs — should hit the depth limit and return null.
            var sb = new System.Text.StringBuilder("_Z1fI");
            for (int i = 0; i < 300; i++)
            {
                sb.Append('J');
            }
            sb.Append('i');
            for (int i = 0; i < 300; i++)
            {
                sb.Append('E');
            }
            sb.Append("Evi");
            Assert.Null(_demangler.Demangle(sb.ToString()));
        }

        [Fact]
        public void WhenDeeplyNestedTemplateThenReturnsNull()
        {
            // Build deeply nested template: 1AIiI...I E...E — should hit the depth limit.
            var sb = new System.Text.StringBuilder("_Z1f");
            for (int i = 0; i < 300; i++)
            {
                sb.Append("1AI");
            }
            sb.Append('i');
            for (int i = 0; i < 300; i++)
            {
                sb.Append('E');
            }
            // Make it a valid-ish function call
            sb.Append("Evi");
            Assert.Null(_demangler.Demangle(sb.ToString()));
        }

        #endregion

        #region Special Names (Truncated)

        [Theory]
        [InlineData("_ZTV")]           // vtable with no type
        [InlineData("_ZTI")]           // typeinfo with no type
        [InlineData("_ZTS")]           // typeinfo name with no type
        [InlineData("_ZTT")]           // VTT with no type
        [InlineData("_ZGV")]           // guard variable with no name
        [InlineData("_ZGR")]           // reference temporary with no name
        public void WhenSpecialNameIsTruncatedThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        #endregion

        #region Number Overflow Protection

        [Fact]
        public void WhenSourceNameLengthOverflowsThenReturnsNull()
        {
            // A source-name with a length that would overflow int should return null
            // rather than producing incorrect results.
            string mangled = "_Z99999999999999999999foov";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenSourceNameLengthIsIntMaxValueThenReturnsNull()
        {
            // int.MaxValue as a source-name length prefix. With _pos > 0 the sum
            // _pos + length wraps negative in unchecked int arithmetic, bypassing
            // the bounds check. Long arithmetic prevents the overflow.
            string mangled = "_Z2147483647a";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenSubstitutionSeqIdOverflowsThenReturnsNull()
        {
            // A substitution with an extremely large base-36 seq-id should return null.
            string mangled = "_Z1fS" + new string('Z', 30) + "_";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenTemplateParamIndexOverflowsThenReturnsNull()
        {
            // A template param with an extremely large index should return null.
            string mangled = "_Z1fIiEvT" + new string('9', 30) + "_";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenSubstitutionSeqIdIsIntMaxValueThenReturnsNull()
        {
            // int.MaxValue in base-36 is "ZIK0ZJ". With seqId == int.MaxValue,
            // (int)seqId + 1 would overflow to int.MinValue. Should return null.
            string mangled = "_Z1fSZIK0ZJ_";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenTemplateParamIndexIsIntMaxValueThenReturnsNull()
        {
            // With paramIndex == int.MaxValue (decimal), (int)paramIndex + 1
            // would overflow to int.MinValue. Should return null.
            string mangled = "_Z1fIiEvT2147483647_";
            Assert.Null(_demangler.Demangle(mangled));
        }

        [Fact]
        public void WhenSignedNumberIsLongMaxValueThenDemangleSucceeds()
        {
            // long.MaxValue (9223372036854775807) is a valid offset and must be accepted.
            Assert.NotNull(_demangler.Demangle("_ZTh9223372036854775807_N1C1fEv"));
        }

        [Fact]
        public void WhenSignedNumberExceedsLongMaxValueThenReturnsNull()
        {
            // An offset with more digits than long.MaxValue overflows and should return null.
            Assert.Null(_demangler.Demangle("_ZTh99999999999999999999_N1C1fEv"));
        }

        #endregion

        #region Regression: Local Name Discriminator Variations (6b3cda34)

        [Theory]
        // Single-digit discriminator at boundary (max single-digit value 9)
        [InlineData("_ZZ3foovE3bar_9", "foo()::bar")]
        // Double-underscore discriminator with a different value than the existing __15_ test
        [InlineData("_ZZ3foovE3bar__99_", "foo()::bar")]
        public void WhenLocalNameHasDiscriminatorThenDiscriminatorIsConsumed(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Multi-Dimensional Array Dimension Order (14aa20d1)

        [Theory]
        // 2D array with different dimensions from existing test
        [InlineData("_Z1fA2_A3_i", "f(int[2][3])")]
        // 3D array with double element type
        [InlineData("_Z1fA5_A10_A20_d", "f(double[5][10][20])")]
        public void WhenMultiDimensionalArrayThenDimensionsInParseOrder(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Expression Dimensions in Array Types (b18090d8)

        [Fact]
        public void WhenArrayDimensionIsExpressionThenDemangleSucceeds()
        {
            // sizeof(int) as array dimension: A <expr:sti> _ <element:i>
            string result = _demangler.Demangle("_Z1fAsti_i");
            Assert.Equal("f(int[sizeof(int)])", result);
        }

        #endregion

        #region Regression: GR Reference Temporary Trailing Suffix (8b41edfa)

        [Theory]
        // Multi-character base-36 seq-id (A = 10 in base-36, i.e., reference temporary #11)
        [InlineData("_ZGR3fooA_", "reference temporary for foo")]
        // Uppercase base-36 seq-id followed by trailing underscore
        [InlineData("_ZGR3fooZ_", "reference temporary for foo")]
        public void WhenGRHasMultiCharSeqIdThenTrailingSuffixConsumed(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Save/Restore Template Arguments in ParseTemplateArgs (357283dc)

        [Fact]
        public void WhenSiblingTemplateArgIsSpecializationThenOuterArgsNotCorrupted()
        {
            // foo<A<int>, int>(...) — parsing A<int> internally sets _templateArguments
            // to ["int"]. Without save/restore, the second sibling arg "int" and subsequent
            // T_ resolution would see stale data.
            // T_ is the return type (discarded), T0_ resolves to "int" (second arg).
            string result = _demangler.Demangle("_Z3fooI1AIiEiET_T0_");
            Assert.Equal("foo<A<int>, int>(int)", result);
        }

        [Fact]
        public void WhenMultipleSiblingSpecializationsThenAllResolveCorrectly()
        {
            // f<A<int>, B<double>>(A<int>, B<double>) — both siblings are template
            // specializations; T_ and T0_ must resolve from the outer template args.
            string result = _demangler.Demangle("_Z1fI1AIiE1BIdEEvT_T0_");
            Assert.Equal("f<A<int>, B<double>>(A<int>, B<double>)", result);
        }

        #endregion

        #region Regression: Expression Fallback N/F Depth Tracking (864942fc)

        [Fact]
        public void WhenExpressionFallbackContainsNestedNameThenNotPrematurelyTerminated()
        {
            // Template arg expression that reaches the fallback and contains N...E
            // (nested-name scope). N must increase depth so the E closing the nested
            // name doesn't prematurely terminate the expression.
            // sr N 1A 1B E 3foo is a scope-resolution expression with a qualified name.
            string result = _demangler.Demangle("_Z1fIXsrN1A1BE3fooEEvi");
            Assert.NotNull(result);
        }

        [Fact]
        public void WhenExpressionFallbackContainsFunctionTypeThenNotPrematurelyTerminated()
        {
            // Template arg expression that reaches the fallback and contains F...E
            // (function type). F must increase depth so the E closing the function
            // type doesn't prematurely terminate the expression.
            string result = _demangler.Demangle("_Z1fIXsrFivE3fooEEvi");
            Assert.NotNull(result);
        }

        #endregion

        #region Regression: Recursion Depth in ParseEncoding and ParseTemplateArg (319db0c4)

        [Fact]
        public void WhenDeeplyNestedLocalNameEncodingsThenReturnsNull()
        {
            // 300 nested local names: each Z triggers ParseLocalName which calls
            // ParseEncoding recursively, hitting the depth limit.
            var sb = new System.Text.StringBuilder("_Z");
            for (int i = 0; i < 300; i++)
            {
                sb.Append('Z');
            }

            sb.Append("3foov");
            for (int i = 0; i < 300; i++)
            {
                sb.Append("E3bar");
            }

            Assert.Null(_demangler.Demangle(sb.ToString()));
        }

        #endregion

        #region Regression: Null Checks in ParseSpecialName and ParseLocalName (3bcfd6a7)

        [Theory]
        // Truncated local name: inner encoding fails (no data after Z)
        [InlineData("_ZZ")]
        // Truncated local name: entity missing after encoding + E
        [InlineData("_ZZ3foovE")]
        public void WhenLocalNameIsTruncatedThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        #endregion

        #region Regression: Stale Template Arguments Fallback (09ae8fea)

        [Fact]
        public void WhenHighTemplateIndexExceedsFunctionArgsThenPlaceholderNotStaleData()
        {
            // foo<double>(double, B<int>, T0) — T0_ (index 1) exceeds the function
            // template args (only 1 entry: "double"). It must produce a placeholder "T0",
            // NOT "int" from B<int>'s inner template args which may have mutated _templateArguments.
            string result = _demangler.Demangle("_Z3fooIdEvT_1BIiET0_");
            Assert.NotNull(result);
            Assert.Contains("T0", result);
            // Must not contain a spurious "int" from B<int>'s stale _templateArguments
            // in the position where T0_ is resolved.
            Assert.Equal("foo<double>(double, B<int>, T0)", result);
        }

        #endregion

        #region Regression: Array Dimension for Function Pointer Element Types (644671df)

        [Fact]
        public void WhenArrayElementIsFunctionPointerThenDimensionIsPresent()
        {
            // A10_ PFivE = array of 10 pointers to function returning int, no params.
            // The dimension [10] must appear in the demangled output.
            string result = _demangler.Demangle("_Z1fA10_PFivE");
            Assert.NotNull(result);
            Assert.Contains("[10]", result);
        }

        [Fact]
        public void WhenArrayOfFunctionPointersWithParamsThenDimensionIsPresent()
        {
            // A5_ PFvidE = array of 5 pointers to function(int, double) returning void.
            string result = _demangler.Demangle("_Z1fA5_PFvidE");
            Assert.NotNull(result);
            Assert.Contains("[5]", result);
        }

        #endregion

        #region Regression: Expression Fallback T_ and S_ Token Handling

        [Fact]
        public void WhenExpressionFallbackContainsTemplateParamThenNotPrematurelyTerminated()
        {
            // An unrecognized operator (tw = throw) followed by T_ in the fallback.
            // The '_' in T_ must not trigger the array-dimension break.
            string result = _demangler.Demangle("_Z1fIiXtwT_EEiv");
            Assert.NotNull(result);
        }

        [Fact]
        public void WhenExpressionFallbackContainsSubstitutionThenNotPrematurelyTerminated()
        {
            // An unrecognized operator (tw = throw) followed by S_ in the fallback.
            // The '_' in S_ must not trigger the array-dimension break.
            string result = _demangler.Demangle("_Z1fIiXtwS_EEiv");
            Assert.NotNull(result);
        }

        [Fact]
        public void WhenArrayDimensionIsExpressionThenUnderscoreDelimiterStillWorks()
        {
            // Regression: sizeof(int) as array dimension uses '_' as the delimiter
            // between the expression and element type. Must still work after the
            // T_/S_ fallback fix.
            string result = _demangler.Demangle("_Z1fAsti_i");
            Assert.Equal("f(int[sizeof(int)])", result);
        }

        #endregion

        #region Regression: Function Call Expression (cl) with E Terminator

        [Theory]
        // 0-arg call: cl <callee> E — bar() with no arguments
        [InlineData("_Z1fIXclL_Z3barEEEEvv", "f<bar()>()")]
        // 1-arg call: cl <callee> <arg> E — bar((int)42)
        [InlineData("_Z1fIXclL_Z3barELi42EEEEvv", "f<bar((int)42)>()")]
        // 2-arg call: cl <callee> <arg> <arg> E — bar((int)1, (int)2)
        [InlineData("_Z1fIXclL_Z3barELi1ELi2EEEEvv", "f<bar((int)1, (int)2)>()")]
        public void WhenFunctionCallExpressionThenCalleeAndArgsParsed(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // operator() in a nested name should still work (not expression context)
        [InlineData("_ZN3FooclEv", "Foo::operator()()")]
        [InlineData("_ZN3FooclEi", "Foo::operator()(int)")]
        public void WhenOperatorCallInNestedNameThenStillDemangles(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Array Dimension Merging for Pointer-to-Array Element Types

        [Theory]
        // Array of 10 pointers-to-array-of-5-ints: int (*[10])[5]
        [InlineData("_Z1fA10_PA5_i", "f(int (*[10])[5])")]
        // Simple multi-dimensional array should still merge correctly
        [InlineData("_Z1fA3_A5_i", "f(int[3][5])")]
        // Pointer to array — baseline (no outer array, no merging)
        [InlineData("_Z1fPA10_i", "f(int (*)[10])")]
        public void WhenArrayElementIsPointerToArrayThenDimensionInsertedInWrapper(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Null Checks in ParseFunctionType, ParsePointerToMemberType, and ParseArrayType

        [Theory]
        // Truncated function type: F followed by E with no return type.
        [InlineData("_Z1fFE")]
        // Truncated pointer-to-member: class type "A" parsed but member type fails on 'E'.
        [InlineData("_Z1fM1AE")]
        // Truncated pointer-to-member: class type is int (i) but member type fails on 'E'.
        [InlineData("_Z1fMiE")]
        public void WhenFunctionOrPointerToMemberTypeIsTruncatedThenReturnsNull(string input)
        {
            Assert.Null(_demangler.Demangle(input));
        }

        #endregion

        #region Regression: Internal Linkage Names (_ZL prefix)

        [Theory]
        // Simple internal-linkage (file-scope static) functions.
        [InlineData("_ZL16FailFastOnAssertv", "FailFastOnAssert()")]
        [InlineData("_ZL15ComputeGCLayoutP11MethodTablePh", "ComputeGCLayout(MethodTable*, unsigned char*)")]
        [InlineData("_ZL12GetClassSyncP11MethodTable", "GetClassSync(MethodTable*)")]
        // Internal-linkage with substitution references in parameter types.
        [InlineData("_ZL22MvidMismatchFatalError5_GUIDS_PKcbS1_", "MvidMismatchFatalError(_GUID, _GUID, char const*, bool, char const*)")]
        // Internal-linkage with nested class parameter types.
        [InlineData("_ZL35GetManagedFormatStringForResourceIDN7CCompRC16ResourceCategoryEjR7SString",
            "GetManagedFormatStringForResourceID(CCompRC::ResourceCategory, unsigned int, SString&)")]
        public void WhenSymbolHasInternalLinkagePrefixThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // Internal-linkage name within a nested name (namespace::L<static-func>).
        [InlineData("_ZN10standaloneL14ScanStackRootsEP6ThreadPFvPP6ObjectP11ScanContextjES6_")]
        public void WhenNestedNameContainsInternalLinkageComponentThenDemangleSucceeds(string mangled)
        {
            Assert.NotNull(_demangler.Demangle(mangled));
        }

        [Theory]
        // Internal-linkage function referenced inside a template expression argument.
        // _ZL appears within Xad L_Z ... E (address-of expression in template arg).
        [InlineData("_ZN11StateHolderIXadL_Z9DoNothingvEEXadL_ZL16EnsurePreemptivevEEED2Ev",
            "StateHolder<&(DoNothing()), &(EnsurePreemptive())>::~StateHolder()")]
        public void WhenExpressionReferencesInternalLinkageFunctionThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: TLS Wrapper and Init Functions (_ZTW / _ZTH)

        [Theory]
        // TLS wrapper for simple thread-local variable.
        [InlineData("_ZTW12t_ThreadType", "TLS wrapper function for t_ThreadType")]
        [InlineData("_ZTW24t_dwCurrentExceptionCode", "TLS wrapper function for t_dwCurrentExceptionCode")]
        // TLS wrapper for nested-name thread-local variable.
        [InlineData("_ZTWN31EventPipeCoreCLRThreadHolderTLS17g_threadHolderTLSE",
            "TLS wrapper function for EventPipeCoreCLRThreadHolderTLS::g_threadHolderTLS")]
        public void WhenSymbolIsTlsWrapperThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        [Theory]
        // TLS init for simple thread-local variable.
        [InlineData("_ZTH22tls_destructionMonitor", "TLS init function for tls_destructionMonitor")]
        // TLS init for nested-name thread-local variable.
        [InlineData("_ZTHN31EventPipeCoreCLRThreadHolderTLS17g_threadHolderTLSE",
            "TLS init function for EventPipeCoreCLRThreadHolderTLS::g_threadHolderTLS")]
        public void WhenSymbolIsTlsInitThenDemangleSucceeds(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Lambda Closure Types (Ul prefix)

        [Theory]
        // Lambda closure type used as template argument.
        [InlineData("_Z18ReadCompressedIntsIZ23ReadInstrumentationDataI28R2RInstrumentationDataReaderEbPKhmRT_EUllE_EbS3_mS4_")]
        // Lambda operator() in a local-name context (const-qualified).
        [InlineData("_ZZ23ReadInstrumentationDataI28R2RInstrumentationDataReaderEbPKhmRT_ENKUllE_clEl")]
        // Lambda closure as template arg to a function template.
        [InlineData("_Z33WriteInstrumentationSchemaToBytesIZ26WriteInstrumentationSchemaPKN11ICorJitInfo24PgoInstrumentationSchemaEmPhmEUlhE_EbS3_mRKT_")]
        // Deeply nested lambda: lambda within lambda within local name.
        [InlineData("_ZZ35ReadInstrumentationSchemaWithLayoutIZ45ReadInstrumentationSchemaWithLayoutIntoSArrayPKhmmP6SArrayIN11ICorJitInfo24PgoInstrumentationSchemaELi1EEEUlRKS4_E_EbS1_mmRT_ENKUlS4_E_clES4_")]
        public void WhenSymbolContainsLambdaClosureTypeThenDemangleSucceeds(string mangled)
        {
            string result = _demangler.Demangle(mangled);
            Assert.NotNull(result);
            Assert.Contains("{lambda(", result);
        }

        #endregion

        #region Regression: Template-Id Substitution in Unscoped Names

        [Theory]
        // Template specialization used in address-of expression inside template args,
        // where the full template-id must be registered as a substitution candidate.
        [InlineData("_ZN10BaseHolderIP8Assembly29CollectibleAssemblyHolderBaseIS1_ELm0EXadL_Z14CompareDefaultIS1_EiT_S5_EEED2Ev",
            "BaseHolder<Assembly*, CollectibleAssemblyHolderBase<Assembly*>, (unsigned long)0, &(CompareDefault<Assembly*>(Assembly*, CompareDefault<Assembly*>))>::~BaseHolder()")]
        [InlineData("_ZN10BaseHolderIP14DomainAssembly29CollectibleAssemblyHolderBaseIS1_ELm0EXadL_Z14CompareDefaultIS1_EiT_S5_EEED2Ev",
            "BaseHolder<DomainAssembly*, CollectibleAssemblyHolderBase<DomainAssembly*>, (unsigned long)0, &(CompareDefault<DomainAssembly*>(DomainAssembly*, CompareDefault<DomainAssembly*>))>::~BaseHolder()")]
        public void WhenTemplateIdIsUsedInExpressionThenSubstitutionTableIsCorrect(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

        #region Regression: Substitution Fallback for Complex Local Names

        [Theory]
        // Local entity within a deeply-nested template function where the substitution table
        // cannot be fully reconstructed. The demangler should return a result with a placeholder
        // rather than failing entirely.
        [InlineData("_ZZN4util4sortI14MethodInModuleNS_4lessIS1_EEEEvPT_S5_T0_EN11sort_helper7CompareEPS1_S8_")]
        public void WhenSubstitutionExceedsTableSizeThenDemangleReturnsPlaceholder(string mangled)
        {
            string result = _demangler.Demangle(mangled);
            Assert.NotNull(result);
            // The result should contain the demangled function name despite the unresolved substitution.
            Assert.Contains("sort_helper::Compare", result);
        }

        #endregion

        #region ParseExprPrimary CV-Qualifier Preservation

        [Theory]
        // When a primary expression references a function via L _Z <encoding> E inside a
        // const-qualified member function template, the const qualifier must survive the
        // inner ParseEncoding call.
        [InlineData("_ZNK3FooIXadL_Z3barEEE3bazEv", "Foo<&(bar)>::baz() const")]
        public void WhenExprPrimaryContainsEncodingThenOuterCvQualifiersArePreserved(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Scope Resolution (sr) with Unqualified Names

        [Theory]
        // The sr expression must support operator names via ParseUnqualifiedName,
        // not just source-names via ParseSourceName.
        [InlineData("_Z1fIXsrieqEE", "f<int::operator==>")]
        public void WhenSrExpressionContainsOperatorThenDemangleSucceeds(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Member Access (dt) with Unqualified Names

        [Theory]
        // The dt expression must support operator names via ParseUnqualifiedName,
        // not just source-names via ParseSourceName.
        [InlineData("_Z1fIXdtT_eqEE", "f<T.operator==>")]
        public void WhenDtExpressionContainsOperatorThenDemangleSucceeds(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        #endregion

        #region GCC/LLVM Linker Suffixes and ELF Versioning

        [Theory]
        [InlineData("_ZN3foo3barEv.cold", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.isra.0", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.lto_priv.1", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.constprop.0", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.localalias", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.part.0", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.isra.0.cold", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.constprop.0.isra.1.cold", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.localalias.lto_priv.0", "foo::bar()")]
        [InlineData("_ZN3foo3barEv.part.0.lto_priv.1.lto_priv.2", "foo::bar()")]
        public void WhenSymbolHasLinkerSuffixThenStripsAndDemangles(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("_ZdlPv@GLIBCXX_3.4", "operator delete(void*)")]
        [InlineData("_Znam@GLIBCXX_3.4", "operator new[](unsigned long)")]
        [InlineData("_ZdlPvm@CXXABI_1.3.9", "operator delete(void*, unsigned long)")]
        [InlineData("_ZSt11_Hash_bytesPKvmm@CXXABI_1.3.5", "std::_Hash_bytes(void const*, unsigned long, unsigned long)")]
        public void WhenSymbolHasElfVersionThenStripsAndDemangles(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        #endregion

        #region ABI Tags

        [Theory]
        [InlineData("_ZN3nut8Variable8getValueB5cxx11Ev", "nut::Variable::getValue[abi:cxx11]()")]
        [InlineData("_Z22lucene_wcstoutf8stringB5cxx11PKwm", "lucene_wcstoutf8string[abi:cxx11](wchar_t const*, unsigned long)")]
        [InlineData("_ZN3foo3barB5cxx11Ei", "foo::bar[abi:cxx11](int)")]
        public void WhenSymbolHasAbiTagThenDemangleSucceeds(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Pack Expansion Types

        [Theory]
        // Dp<type> — parameter pack expansion (T...).
        [InlineData(
            "_ZN8libvisio11make_uniqueINS_7XForm1DEJEEESt10unique_ptrIT_St14default_deleteIS3_EEDpOT0_",
            null)]
        public void WhenTypeHasPackExpansionThenDemangleSucceeds(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.NotNull(result);
            if (expected != null)
            {
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public void WhenSharedPtrConstructorHasPackExpansionThenDemangleSucceeds()
        {
            // This is a real-world symbol from librevenge.
            string result = _demangler.Demangle(
                "_ZNSt10shared_ptrI14TagOpenElementEC1ISaIvEJRA10_KcEEESt20_Sp_alloc_shared_tagIT_EDpOT0_");
            Assert.NotNull(result);
            Assert.Contains("shared_ptr", result);
            Assert.Contains("TagOpenElement", result);
        }

        #endregion

        #region C4/C5 Constructor Variants

        [Theory]
        [InlineData("_ZN3Foo3BarC4Ev", "Foo::Bar::Bar()")]
        [InlineData("_ZN3Foo3BarC5Ev", "Foo::Bar::Bar()")]
        public void WhenConstructorUsesC4OrC5VariantThenDemangleSucceeds(string mangled, string expected)
        {
            string result = _demangler.Demangle(mangled);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void WhenRealWorldC4ConstructorInLambdaThenDemangleSucceeds()
        {
            // Real-world symbol from libmemcached: lambda inside C4 constructor.
            string result = _demangler.Demangle(
                "_ZNSt17_Function_handlerIFbR14client_optionsRNS0_15extended_optionEEZNS0_C4EPKcS6_S6_S6_EUlS1_S3_E_E9_M_invokeERKSt9_Any_dataS1_S3_");
            Assert.NotNull(result);
            Assert.Contains("_Function_handler", result);
        }

        [Fact]
        public void WhenThreadStateImplUsesC4ThenDemangleSucceeds()
        {
            string result = _demangler.Demangle(
                "_ZNSt6thread11_State_implINS_8_InvokerISt5tupleIJZN14thread_contextC4ERK14client_optionsRK12memcached_stRK9keyval_stEUlvE_EEEEE6_M_runEv");
            Assert.NotNull(result);
            Assert.Contains("_State_impl", result);
            Assert.Contains("_M_run", result);
        }

        #endregion

        #region Unresolved Names in Expressions

        [Fact]
        public void WhenDecltypeContainsFunctionCallWithUnresolvedNameThenDemangleSucceeds()
        {
            // Real-world symbol: std::operator<=> with decltype return type containing
            // a function call to a bare source-name (__char_traits_cmp_cat<T0_>).
            string result = _demangler.Demangle(
                "_ZStssIcSt11char_traitsIcESaIcEEDTcl21__char_traits_cmp_catIT0_ELi0EEERKNSt7__cxx1112basic_stringIT_S3_T1_EESB_");
            Assert.NotNull(result);
            Assert.Contains("operator<=>", result);
        }

        #endregion

        #region Benchmark: Real-World Symbol Coverage

        [Theory]
        // Benchmark: Operator new/delete with nothrow (C++11)
        [InlineData("_ZnwmRKSt9nothrow_t", "operator new(unsigned long, std::nothrow_t const&)")]
        [InlineData("_ZnamRKSt9nothrow_t", "operator new[](unsigned long, std::nothrow_t const&)")]
        [InlineData("_ZdlPvRKSt9nothrow_t", "operator delete(void*, std::nothrow_t const&)")]
        [InlineData("_ZdaPvRKSt9nothrow_t", "operator delete[](void*, std::nothrow_t const&)")]

        // Benchmark: Sized operator delete (C++14)
        [InlineData("_ZdlPvm", "operator delete(void*, unsigned long)")]
        [InlineData("_ZdaPvm", "operator delete[](void*, unsigned long)")]

        // Benchmark: Function pointer parameters (real-world Lua/JACK library symbols)
        [InlineData("_Z11lua_atpanicP9lua_StatePFiS0_E", "lua_atpanic(lua_State*, int (*)(lua_State*))")]
        [InlineData("_Z11lua_sethookP9lua_StatePFvS0_P9lua_DebugEii", "lua_sethook(lua_State*, void (*)(lua_State*, lua_Debug*), int, int)")]
        [InlineData("_Z10luaD_pcallP9lua_StatePFvS0_PvES1_ll", "luaD_pcall(lua_State*, void (*)(lua_State*, void*), void*, long, long)")]

        // Benchmark: ELF versioned symbols (@GLIBCXX, @CXXABI)
        [InlineData("_ZdlPvm@CXXABI_1.3.9", "operator delete(void*, unsigned long)")]
        [InlineData("_ZnwmRKSt9nothrow_t@GLIBCXX_3.4", "operator new(unsigned long, std::nothrow_t const&)")]

        // Benchmark: GCC linker suffixes on complex symbols
        [InlineData("_Z10luaD_pcallP9lua_StatePFvS0_PvES1_ll.constprop.0", "luaD_pcall(lua_State*, void (*)(lua_State*, void*), void*, long, long)")]
        [InlineData("_Z10luaD_throwP9lua_Statei.cold", "luaD_throw(lua_State*, int)")]

        // Benchmark: Variadic pack expansion (Dp)
        [InlineData("_ZN8libvisio11make_uniqueINS_11ForeignDataEJRS1_EEESt10unique_ptrIT_St14default_deleteIS4_EEDpOT0_", "libvisio::make_unique<libvisio::ForeignData, libvisio::ForeignData&>(libvisio::ForeignData&&&...)")]

        // Benchmark: Address-of in template arguments (Xad + literal function references)
        [InlineData("_ZN5Exiv28Internal19newTiffBinaryArray0IXadL_ZNS0_10canonCsCfgEEELi1EL_ZNS0_10canonCsDefEEEESt8auto_ptrINS0_13TiffComponentEEtNS0_5IfdIdE", "Exiv2::Internal::newTiffBinaryArray0<&(Exiv2::Internal::canonCsCfg), (int)1, Exiv2::Internal::canonCsDef>(unsigned short, Exiv2::Internal::IfdId)")]

        // Benchmark: Real-world function signatures
        [InlineData("_Z10luaF_closeP9lua_StateP10StackValueii", "luaF_close(lua_State*, StackValue*, int, int)")]
        [InlineData("_Z10luaL_errorP9lua_StatePKcz", "luaL_error(lua_State*, char const*, ...)")]
        [InlineData("_Z10luaL_unrefP9lua_Stateii", "luaL_unref(lua_State*, int, int)")]
        [InlineData("_ZL11PutDoubleBEdPv", "PutDoubleBE(double, void*)")]
        [InlineData("_ZL25StartNamespaceDeclHandlerPvPKcS1_", "StartNamespaceDeclHandler(void*, char const*, char const*)")]

        // Benchmark: Lambda with discriminators in std::_Function_handler
        [InlineData("_ZNSt17_Function_handlerIFbR14client_optionsRNS0_15extended_optionEEZ4mainEUlS1_S3_E0_E9_M_invokeERKSt9_Any_dataS1_S3_", "std::_Function_handler<bool (client_options&, std::_Function_handler::extended_option&), main::{lambda(client_options, std::_Function_handler::extended_option)#2}>::_M_invoke(std::_Any_data const&, client_options, std::_Function_handler::extended_option)")]
        public void WhenBenchmarkSymbolThenDemangleMatchesExpected(string mangled, string expected)
        {
            Assert.Equal(expected, _demangler.Demangle(mangled));
        }

        #endregion

    }
}
