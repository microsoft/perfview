# Coding Standard in the PerfView Codebase

If you are going to contribute to a codebase, you need to 'follow suit'
and conform to the standards that are already in place.   Here is what
PerfView uses.  

## Indenting and other spacing conventions. 

The PerfView codebase was developed using Visual Studio, and uses
indenting and spacing standards that are the default in Visual Studio.
You can use Ctrl-K Ctrl-D (reformat) to make your code conform to 
this.   

## Layout of a Class

Items in a class should be ordered and structured to make reading the
as a **public contract** easy. In particular:

1. All private items come AFTER all public ones and are surrounded
by a '#region private' grouping.   This makes Visual Studio's 
outlining feature (Ctrl-M Ctrl-O) collapse things so that you 
only see the public contract for the class.  

2. Public methods should be ordered so that constructors or other
'generators' are first, then properties, then methods.

3. To the degree possible the most important/common methods should
come first in the class, and methods that are used together 
should be near each other.   

4. Fields should be private and placed TOGETHER, LAST in the class 
(in the #region private). That makes it relatively easy for 
developers to find all the state in an object (since that is what
really defines its semantics).

## Naming conventions

1. We follow standard .NET Naming conventions (PascalCase for types,
   methods, and properties; camelCase for parameters and local variables)

2. Private instance field names begin with a `m_` (member).   It is also 
   acceptable to use the class library convention of omitting the m (thus prefix
   instance fields begin with `_`) If the field is static the prefix is `s_`.
   Embedding the type in the variable (Hungarian notation) is NOT used.

3. Pick descriptive names. Visual Studio makes it easy to rename a 
   variable so fix the name if it 'morphed' as the code was written. 

## Minimum Commenting

PerfView is probably commented more than most code bases. We wish
to keep it that way. Here is what is expected.

1. If the type is public (outside the assembly) it needs a comment
       and all its public members need comments.  

2. Comments before declarations must follow the XML commenting conventions using three slashes to indicate this.   
       However, typically it is not necessary to explain each parameter
       to a method (since you gave them really good names, right?).   
       You can simply use the `summary` tag and omit the rest (but put
       the important information in the summary tag). 

3. Field variables of a class typically DO need commenting. This is
       especially true if there is some condition (invariant) that is maintained
       for that variable. These are VERY valuable to the document.   
 
## When in Doubt

When in doubt, make your code look like the code around it.   You can't
go too far wrong if you do that.   

#### See Also
* [PerfView ReadMe](../README.md)
* [PerfView Contribution Guide](../CONTRIBUTING.md)
