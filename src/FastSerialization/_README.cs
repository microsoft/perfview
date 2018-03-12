//  Copyright (c) Microsoft Corporation.  All rights reserved.
// Welcome to the Utilities code base. This _README.cs file is your table of contents.
// 
// You will notice that the code is littered with code: qualifiers. If you install the 'hyperAddin' for
// Visual Studio, these qualifiers turn into hyperlinks that allow easy cross references. The hyperAddin is
// available on http://www.codeplex.com/hyperAddin
// 
// -------------------------------------------------------------------------------------
// Overview of files
// 
// * file:GrowableArray.cs - holds a VERY LEAN implentation of a variable sized array (it is
//     striped down, fast version of List<T>. There is never a reason to implement this logic by hand.
//     
// * file:StreamUtilities.cs holds code:StreamUtilities which knows how to copy a stream.
// 
// * file:FastSerialization.cs - holds defintions for code:FastSerialization.Serializer and
//     code:FastSerialization.Deserializer which are general purpose, very fast and efficient ways of dumping
//     an object graph to a stream (typically a file). It is used as a very convenient, versionable,
//     efficient and extensible file format.
// 
// * file:StreamReaderWriter.cs - holds concreate subclasses of
//     code:FastSerialization.IStreamReader and code:FastSerialization.IStreamWriter, which allow you to
//     write files a byte, chareter, int, or string at a time efficiently.
