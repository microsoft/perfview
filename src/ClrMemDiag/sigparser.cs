using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    struct SigParser
    {
        byte[] m_sig;
        int m_len;
        int m_offs;

        public SigParser(byte[] sig, int len)
        {
            m_sig = sig;
            m_len = len;
            m_offs = 0;
        }

        public SigParser(byte[] sig, int len, int offs)
        {
            m_sig = sig;
            m_len = len;
            m_offs = offs;
        }

        public SigParser(SigParser rhs)
        {
            m_sig = rhs.m_sig;
            m_len = rhs.m_len;
            m_offs = rhs.m_offs;
        }

        public SigParser(IntPtr sig, int len)
        {
            if (len != 0)
            {
                m_sig = new byte[len];
                Marshal.Copy(sig, m_sig, 0, m_sig.Length);
            }
            else
            {
                m_sig = null;
            }



            m_len = len;
            m_offs = 0;
        }

        public bool IsNull()
        {
            return m_sig == null;
        }

        private void CopyFrom(SigParser rhs)
        {
            m_sig = rhs.m_sig;
            m_len = rhs.m_len;
            m_offs = rhs.m_offs;
        }


        void SkipBytes(int bytes)
        {
            Debug.Assert(bytes <= m_len);
            m_offs += bytes;
            m_len -= bytes;
            Debug.Assert(m_len <= 0 || m_offs < m_sig.Length);
        }

        bool SkipInt()
        {
            int tmp;
            return GetData(out tmp);
        }

        public bool GetData(out int data)
        {
            int size = 0;
            if (UncompressData(out data, out size))
            {
                SkipBytes(size);
                return true;
            }

            return false;
        }
        
        bool GetByte(out byte data)
        {
            if (m_len <= 0)
            {
                data = 0xcc;
                return false;
            }

            data = m_sig[m_offs];
            SkipBytes(1);
            return true;
        }

        bool PeekByte(out byte data)
        {
            if (m_len <= 0)
            {
                data = 0xcc;
                return false;
            }

            data = m_sig[m_offs];
            return true;
        }

        bool GetElemTypeSlow(out int etype)
        {
            SigParser sigTemp = new SigParser(this);
            if (sigTemp.SkipCustomModifiers())
            {
                byte elemType;
                if (sigTemp.GetByte(out elemType))
                {
                    etype = elemType;
                    this.CopyFrom(sigTemp);
                    return true;
                }
            }

            etype = 0;
            return false;
        }

        public bool GetElemType(out int etype)
        {
            if (m_len > 0)
            {
                byte type = m_sig[m_offs];

                if (type < ELEMENT_TYPE_CMOD_REQD) // fast path with no modifiers: single byte
                {
                    etype = type;
                    SkipBytes(1);
                    return true;
                }
            }

            // Slower/normal path
            return GetElemTypeSlow(out etype);
        }

        
        public bool PeekCallingConvInfo(out int data)
        {
            return PeekByte(out data);
        }

        // Note: Calling convention is always one byte, not four bytes
        public bool GetCallingConvInfo(out int data) 
        {
            if (PeekByte(out data))
            {
                SkipBytes(1);
                return true;
            }

            return false;
        }   

        bool GetCallingConv(out int data)
        {
            if (GetCallingConvInfo(out data))
            {
                data &= IMAGE_CEE_CS_CALLCONV_MASK;
                return true;
            }

            return false;
        }

        bool PeekData(out int data)
        {
            int size;
            return UncompressData(out data, out size);
        }

        bool PeekElemTypeSlow(out int etype)
        {
            SigParser sigTemp = new SigParser(this);
            return sigTemp.GetElemType(out etype);
        }

        public bool PeekElemType(out int etype)
        {
            if (m_len > 0)
            {
                byte type = m_sig[m_offs];
                if (type < ELEMENT_TYPE_CMOD_REQD)
                {
                    etype = type;
                    return true;
                }
            }

            return PeekElemTypeSlow(out etype);
        }
        
        bool PeekElemTypeSize(out int pSize)
        {
            pSize = 0;
            SigParser sigTemp = new SigParser(this);

            if (!sigTemp.SkipAnyVASentinel())
                return false;

            byte bElementType = 0;
            if (!sigTemp.GetByte(out bElementType))
                return false;


            switch (bElementType)
            {
            case ELEMENT_TYPE_I8:
            case ELEMENT_TYPE_U8:
            case ELEMENT_TYPE_R8:
                pSize = 8;
                break;

            case ELEMENT_TYPE_I4:
            case ELEMENT_TYPE_U4:
            case ELEMENT_TYPE_R4:
                pSize = 4;
                break;

            case ELEMENT_TYPE_I2:
            case ELEMENT_TYPE_U2:
            case ELEMENT_TYPE_CHAR:
                pSize = 2;
                break;

            case ELEMENT_TYPE_I1:
            case ELEMENT_TYPE_U1:
            case ELEMENT_TYPE_BOOLEAN:
                pSize = 1;
                break;

            case ELEMENT_TYPE_I:
            case ELEMENT_TYPE_U:
            case ELEMENT_TYPE_STRING:
            case ELEMENT_TYPE_PTR:
            case ELEMENT_TYPE_BYREF:
            case ELEMENT_TYPE_CLASS:
            case ELEMENT_TYPE_OBJECT:
            case ELEMENT_TYPE_FNPTR:
            case ELEMENT_TYPE_TYPEDBYREF:
            case ELEMENT_TYPE_ARRAY:
            case ELEMENT_TYPE_SZARRAY:
                pSize = IntPtr.Size;
                break;

            case ELEMENT_TYPE_VOID:
                break;

            case ELEMENT_TYPE_END:
            case ELEMENT_TYPE_CMOD_REQD:
            case ELEMENT_TYPE_CMOD_OPT:
            case ELEMENT_TYPE_VALUETYPE:
                Debug.Fail("Asked for the size of an element that doesn't have a size!");
                return false;

            default:
                Debug.Fail("CorSigGetElementTypeSize given invalid signature to size!");
                return false;
            }

            return true;;
        }

        bool AtSentinel()
        {
            if (m_len > 0)
                return m_sig[m_offs] == ELEMENT_TYPE_SENTINEL;
            else
                return false;
        }

        bool GetToken(out int token)
        {
            int size;
            if (UncompressToken(out token, out size))
            {
                SkipBytes(size);
                return true;
            }

            return false;
        }

        public bool SkipCustomModifiers()
        {
            SigParser sigTemp = new SigParser(this);

            if (!sigTemp.SkipAnyVASentinel())
                return false;

            byte bElementType = 0;
            if (!sigTemp.PeekByte(out bElementType))
                return false;


            while ((ELEMENT_TYPE_CMOD_REQD == bElementType) || (ELEMENT_TYPE_CMOD_OPT == bElementType))
            {
                sigTemp.SkipBytes(1);

                int token;
                if (!sigTemp.GetToken(out token))
                    return false;

                if (!sigTemp.PeekByte(out bElementType))
                    return false;
            }

            // Following custom modifiers must be an element type value which is less than ELEMENT_TYPE_MAX, or one of the other element types
            // that we support while parsing various signatures
            if (bElementType >= ELEMENT_TYPE_MAX)
            {
                switch (bElementType)
                {
                case ELEMENT_TYPE_PINNED:
                    break;
                default:
                    return false;
                }
            }

            CopyFrom(sigTemp);
            return true;
        }

        bool SkipFunkyAndCustomModifiers()
        {
            SigParser sigTemp = new SigParser(this);
            if (!sigTemp.SkipAnyVASentinel())
                return false;


            byte bElementType = 0;

            if (!sigTemp.PeekByte(out bElementType))
                return false;

            while (ELEMENT_TYPE_CMOD_REQD == bElementType || 
                   ELEMENT_TYPE_CMOD_OPT == bElementType ||
                   ELEMENT_TYPE_MODIFIER == bElementType ||
                   ELEMENT_TYPE_PINNED == bElementType)
            {
                sigTemp.SkipBytes(1);
                
                int token;
                if (!sigTemp.GetToken(out token))
                    return false;

                if (!sigTemp.PeekByte(out bElementType))
                    return false;
            }

            // Following custom modifiers must be an element type value which is less than ELEMENT_TYPE_MAX, or one of the other element types
            // that we support while parsing various signatures
            if (bElementType >= ELEMENT_TYPE_MAX)
            {
                switch (bElementType)
                {
                case ELEMENT_TYPE_PINNED:
                    break;
                default:
                    return false;
                }
            }

            CopyFrom(sigTemp);
            return true;
        }// SkipFunkyAndCustomModifiers

        bool SkipAnyVASentinel()
        {
            byte bElementType = 0;
            if (!PeekByte(out bElementType))
                return false;

            if (bElementType == ELEMENT_TYPE_SENTINEL)
                SkipBytes(1);

            return true;
        }// SkipAnyVASentinel

        private static bool IsPrimitive(ClrElementType cet)
        {
            return cet >= ClrElementType.Boolean && cet <= ClrElementType.Double
                || cet == ClrElementType.NativeInt || cet == ClrElementType.NativeUInt
                || cet == ClrElementType.Pointer || cet == ClrElementType.FunctionPointer;
        }

        public bool SkipExactlyOne()
        {
            int typ;
            if (!GetElemType(out typ))
                return false;

            int tmp;
            if (!IsPrimitive((ClrElementType)typ))
            {
                switch (typ)
                {
                    default:
                        return false;

                    case ELEMENT_TYPE_VAR:
                    case ELEMENT_TYPE_MVAR:
                        if (!GetData(out tmp))
                            return false;
                        break;

                    case ELEMENT_TYPE_OBJECT:
                    case ELEMENT_TYPE_STRING:
                    case ELEMENT_TYPE_TYPEDBYREF:
                        break;

                    case ELEMENT_TYPE_BYREF:
                    case ELEMENT_TYPE_PTR:
                    case ELEMENT_TYPE_PINNED:
                    case ELEMENT_TYPE_SZARRAY:
                        if (!SkipExactlyOne())
                            return false;
                        break;

                    case ELEMENT_TYPE_VALUETYPE:
                    case ELEMENT_TYPE_CLASS:
                        if (!GetToken(out tmp))
                            return false;
                        break;

                    case ELEMENT_TYPE_FNPTR:
                        if (!SkipSignature())
                            return false;
                        break;

                    case ELEMENT_TYPE_ARRAY:
                        // Skip element type
                        if (!SkipExactlyOne())
                            return false;

                        // Get rank;
                        int rank;
                        if (!GetData(out rank))
                            return false;

                        if (rank > 0)
                        {
                            int sizes;
                            if (!GetData(out sizes))
                                return false;

                            while (sizes-- != 0)
                                if (!GetData(out tmp))
                                    return false;

                            int bounds;
                            if (!GetData(out bounds))
                                return false;
                            while (bounds -- != 0)
                                if (!GetData(out tmp))
                                    return false;
                        }
                        break;

                    case ELEMENT_TYPE_SENTINEL:
                        // Should be unreachable since GetElem strips it
                        break;

                    case ELEMENT_TYPE_INTERNAL:
                        if (!GetData(out tmp))
                            return false;
                        break;

                    case ELEMENT_TYPE_GENERICINST:
                        // Skip generic type
                        if (!SkipExactlyOne())
                            return false;

                        // Get number of parameters
                        int argCnt;
                        if (!GetData(out argCnt))
                            return false;

                        // Skip the parameters
                        while (argCnt-- != 0)
                            SkipExactlyOne();
                      break;

                }
            }

            return true;
        }

        bool SkipMethodHeaderSignature(out int pcArgs)
        {
            pcArgs = 0;

            // Skip calling convention
            int uCallConv, tmp;
            if (!GetCallingConvInfo(out uCallConv))
                return false;

            if ((uCallConv == IMAGE_CEE_CS_CALLCONV_FIELD) || 
                (uCallConv == IMAGE_CEE_CS_CALLCONV_LOCAL_SIG))
            {
                return false;
            }

            // Skip type parameter count
            if ((uCallConv & IMAGE_CEE_CS_CALLCONV_GENERIC) == IMAGE_CEE_CS_CALLCONV_GENERIC)
                if (!GetData(out tmp))
                    return false;

            // Get arg count;
            if (!GetData(out pcArgs))
                return false;

            // Skip return type;
            if (!SkipExactlyOne())
                return false;

            return true;
        } // SigParser::SkipMethodHeaderSignature


        private bool SkipSignature()
        {
            int args;
            if (!SkipMethodHeaderSignature(out args))
                return false;


            // Skip args.
            while (args-- > 0)
                if (!SkipExactlyOne())
                    return false;

            return false;
        }

        private bool UncompressToken(out int token, out int size)
        {
            if (!UncompressData(out token, out size))
                return false;

            var tkType = g_tkCorEncodeToken[token & 3];
            token = (token >> 2) | tkType;
            return true;
        }

        byte GetSig(int offs)
        {
            Debug.Assert(offs < m_len);
            return m_sig[m_offs + offs];
        }

        bool UncompressData(out int pDataOut, out int pDataLen)
        {
            pDataOut = 0;
            pDataLen = 0;

            if (m_len <= 0)
                return false;

            byte byte0 = GetSig(0);

            // Smallest.
            if ((byte0 & 0x80) == 0x00)       // 0??? ????
            {
                if (m_len < 1)
                {
                    return false;
                }
                else
                {
                    pDataOut = byte0;
                    pDataLen = 1; 
                }
            }
            // Medium.
            else if ((byte0 & 0xC0) == 0x80)  // 10?? ????
            {
                if (m_len < 2)
                {
                    return false;
                }
                else
                {
                    pDataOut = (int)(((byte0 & 0x3f) << 8 | GetSig(1)));
                    pDataLen = 2; 
                }
            }
            else if ((byte0 & 0xE0) == 0xC0)      // 110? ????
            {
                if (m_len < 4)
                {
                    return false;
                }
                else
                {
                    pDataOut = (int)(((byte0 & 0x1f) << 24 | GetSig(1) << 16 | GetSig(2) << 8 | GetSig(3)));
                    pDataLen = 4; 
                }
            }
            else // We don't recognize this encoding
            {
                return false;
            }
    
            return true;
        }




        private bool PeekByte(out int data)
        {
            byte tmp;
            if (!PeekByte(out tmp))
            {
                data = 0xcc;
                return false;
            }

            data = tmp;
            return true;
        }



        const int mdtModule = 0x00000000;       //
        const int mdtTypeRef = 0x01000000;       //
        const int mdtTypeDef = 0x02000000;       //
        const int mdtFieldDef = 0x04000000;       //
        const int mdtMethodDef = 0x06000000;       //
        const int mdtParamDef = 0x08000000;       //
        const int mdtInterfaceImpl = 0x09000000;       //
        const int mdtMemberRef = 0x0a000000;       //
        const int mdtCustomAttribute = 0x0c000000;       //
        const int mdtPermission = 0x0e000000;       //
        const int mdtSignature = 0x11000000;       //
        const int mdtEvent = 0x14000000;       //
        const int mdtProperty = 0x17000000;       //
        const int mdtMethodImpl = 0x19000000;       //
        const int mdtModuleRef = 0x1a000000;       //
        const int mdtTypeSpec = 0x1b000000;       //
        const int mdtAssembly = 0x20000000;       //
        const int mdtAssemblyRef = 0x23000000;       //
        const int mdtFile = 0x26000000;       //
        const int mdtExportedType = 0x27000000;       //
        const int mdtManifestResource = 0x28000000;       //
        const int mdtGenericParam = 0x2a000000;       //
        const int mdtMethodSpec = 0x2b000000;       //
        const int mdtGenericParamConstraint = 0x2c000000;

        const int mdtString = 0x70000000;       //
        const int mdtName = 0x71000000;       //
        const int mdtBaseType = 0x72000000;       // Leave this on the high end value. This does not correspond to metadata table

        readonly static int[] g_tkCorEncodeToken = new int[] { mdtTypeDef, mdtTypeRef, mdtTypeSpec, mdtBaseType };

        const int IMAGE_CEE_CS_CALLCONV_DEFAULT       = 0x0;

        public const int IMAGE_CEE_CS_CALLCONV_VARARG = 0x5;
        public const int IMAGE_CEE_CS_CALLCONV_FIELD = 0x6;
        public const int IMAGE_CEE_CS_CALLCONV_LOCAL_SIG = 0x7;
        public const int IMAGE_CEE_CS_CALLCONV_PROPERTY = 0x8;
        public const int IMAGE_CEE_CS_CALLCONV_UNMGD = 0x9;
        public const int IMAGE_CEE_CS_CALLCONV_GENERICINST = 0xa;
        public const int IMAGE_CEE_CS_CALLCONV_NATIVEVARARG = 0xb;
        public const int IMAGE_CEE_CS_CALLCONV_MAX = 0xc;

        public const int IMAGE_CEE_CS_CALLCONV_MASK = 0x0f;
        public const int IMAGE_CEE_CS_CALLCONV_HASTHIS = 0x20;
        public const int IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS = 0x40;
        public const int IMAGE_CEE_CS_CALLCONV_GENERIC = 0x10;


        const int ELEMENT_TYPE_END            = 0x0;
        const int ELEMENT_TYPE_VOID           = 0x1;
        const int ELEMENT_TYPE_BOOLEAN        = 0x2;
        const int ELEMENT_TYPE_CHAR           = 0x3;
        const int ELEMENT_TYPE_I1             = 0x4;
        const int ELEMENT_TYPE_U1             = 0x5;
        const int ELEMENT_TYPE_I2             = 0x6;
        const int ELEMENT_TYPE_U2             = 0x7;
        const int ELEMENT_TYPE_I4             = 0x8;
        const int ELEMENT_TYPE_U4             = 0x9;
        const int ELEMENT_TYPE_I8             = 0xa;
        const int ELEMENT_TYPE_U8             = 0xb;
        const int ELEMENT_TYPE_R4             = 0xc;
        const int ELEMENT_TYPE_R8             = 0xd;
        const int ELEMENT_TYPE_STRING         = 0xe;
    
        const int ELEMENT_TYPE_PTR            = 0xf;
        const int ELEMENT_TYPE_BYREF          = 0x10;
    
        const int ELEMENT_TYPE_VALUETYPE      = 0x11;
        const int ELEMENT_TYPE_CLASS          = 0x12;
        const int ELEMENT_TYPE_VAR            = 0x13;
        const int ELEMENT_TYPE_ARRAY          = 0x14;
        const int ELEMENT_TYPE_GENERICINST    = 0x15;
        const int ELEMENT_TYPE_TYPEDBYREF     = 0x16;

        const int ELEMENT_TYPE_I              = 0x18;
        const int ELEMENT_TYPE_U              = 0x19;
        const int ELEMENT_TYPE_FNPTR          = 0x1B;
        const int ELEMENT_TYPE_OBJECT         = 0x1C;
        const int ELEMENT_TYPE_SZARRAY        = 0x1D;
        const int ELEMENT_TYPE_MVAR           = 0x1e;

        const int ELEMENT_TYPE_CMOD_REQD      = 0x1F;
        const int ELEMENT_TYPE_CMOD_OPT       = 0x20;

        const int ELEMENT_TYPE_INTERNAL       = 0x21;
        const int ELEMENT_TYPE_MAX            = 0x22;

        const int ELEMENT_TYPE_MODIFIER       = 0x40;
        const int ELEMENT_TYPE_SENTINEL       = 0x01 | ELEMENT_TYPE_MODIFIER;
        const int ELEMENT_TYPE_PINNED = 0x05 | ELEMENT_TYPE_MODIFIER;
    }

    /// <summary>
    /// This is a representation of the metadata element type.  These values
    /// directly correspond with Clr's CorElementType.
    /// </summary>
    public enum ClrElementType
    {
        /// <summary>
        /// Not one of the other types.
        /// </summary>
        Unknown = 0x0,
        /// <summary>
        /// ELEMENT_TYPE_BOOLEAN
        /// </summary>
        Boolean = 0x2,
        /// <summary>
        /// ELEMENT_TYPE_CHAR
        /// </summary>
        Char = 0x3,

        /// <summary>
        /// ELEMENT_TYPE_I1
        /// </summary>
        Int8 = 0x4,

        /// <summary>
        /// ELEMENT_TYPE_U1
        /// </summary>
        UInt8 = 0x5,

        /// <summary>
        /// ELEMENT_TYPE_I2
        /// </summary>
        Int16 = 0x6,

        /// <summary>
        /// ELEMENT_TYPE_U2
        /// </summary>
        UInt16 = 0x7,

        /// <summary>
        /// ELEMENT_TYPE_I4
        /// </summary>
        Int32 = 0x8,

        /// <summary>
        /// ELEMENT_TYPE_U4
        /// </summary>
        UInt32 = 0x9,

        /// <summary>
        /// ELEMENT_TYPE_I8
        /// </summary>
        Int64 = 0xa,

        /// <summary>
        /// ELEMENT_TYPE_U8
        /// </summary>
        UInt64 = 0xb,

        /// <summary>
        /// ELEMENT_TYPE_R4
        /// </summary>
        Float = 0xc,

        /// <summary>
        /// ELEMENT_TYPE_R8
        /// </summary>
        Double = 0xd,

        /// <summary>
        /// ELEMENT_TYPE_STRING
        /// </summary>
        String = 0xe,

        /// <summary>
        /// ELEMENT_TYPE_PTR
        /// </summary>
        Pointer = 0xf,

        /// <summary>
        /// ELEMENT_TYPE_VALUETYPE
        /// </summary>
        Struct = 0x11,

        /// <summary>
        /// ELEMENT_TYPE_CLASS
        /// </summary>
        Class = 0x12,

        /// <summary>
        /// ELEMENT_TYPE_ARRAY
        /// </summary>
        Array = 0x14,

        /// <summary>
        /// ELEMENT_TYPE_I
        /// </summary>
        NativeInt = 0x18,

        /// <summary>
        /// ELEMENT_TYPE_U
        /// </summary>
        NativeUInt = 0x19,

        /// <summary>
        /// ELEMENT_TYPE_FNPTR
        /// </summary>
        FunctionPointer = 0x1B,

        /// <summary>
        /// ELEMENT_TYPE_OBJECT
        /// </summary>
        Object = 0x1C,

        /// <summary>
        /// ELEMENT_TYPE_SZARRAY
        /// </summary>
        SZArray = 0x1D,
    }

}
