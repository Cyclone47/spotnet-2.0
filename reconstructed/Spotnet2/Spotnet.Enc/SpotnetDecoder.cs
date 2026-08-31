using System;
using System.Runtime.CompilerServices;

namespace SpotnetEnc
{
    public class SpotnetDecoder
    {
        public void Init()
        {
            // Initializer for decoder lookup tables / SIMD state if required
        }

        public unsafe uint Decode(byte[] args, byte[] result, int start, uint arg_len)
        {
            if (args == null || result == null || arg_len == 0)
                return 0;

            uint written = 0;
            int end = (int)Math.Min((long)start + arg_len, (long)args.Length);
            int maxOut = result.Length;

            fixed (byte* pSrc = &args[0])
            fixed (byte* pDst = &result[0])
            {
                byte* src = pSrc + start;
                byte* srcEnd = pSrc + end;
                byte* dst = pDst;
                byte* dstEnd = pDst + maxOut;

                while (src < srcEnd && dst < dstEnd)
                {
                    byte b = *src++;
                    if (b == (byte)'\r' || b == (byte)'\n')
                    {
                        continue;
                    }

                    if (b == (byte)'=') // Escape character in yEnc
                    {
                        if (src >= srcEnd) break;
                        byte next = *src++;
                        if (next == (byte)'\r' || next == (byte)'\n')
                        {
                            if (src < srcEnd && (*src == (byte)'\r' || *src == (byte)'\n'))
                                src++;
                            continue;
                        }
                        *dst++ = (byte)((next - 64 - 42) & 0xFF);
                        written++;
                    }
                    else
                    {
                        *dst++ = (byte)((b - 42) & 0xFF);
                        written++;
                    }
                }
            }

            return written;
        }
    }
}
