using System;
using System.Text;
using SpotnetEnc;
using Xunit;

namespace Spotnet.Tests
{
    public class YEncDecoderTests
    {
        [Fact]
        public void SpotnetDecoder_DecodesSimpleYEncData()
        {
            // Simple yEnc encoding test:
            // Original byte 0 -> encoded (0 + 42) = 42 ('*')
            // Original byte 1 -> encoded (1 + 42) = 43 ('+')
            string yencBody = "*+\r\n";
            byte[] inputBytes = Encoding.ASCII.GetBytes(yencBody);
            byte[] outputBuffer = new byte[10];

            var decoder = new SpotnetDecoder();
            uint decodedBytes = decoder.Decode(inputBytes, outputBuffer, 0, (uint)inputBytes.Length);

            Assert.Equal(2u, decodedBytes);
            Assert.Equal(0, outputBuffer[0]);
            Assert.Equal(1, outputBuffer[1]);
        }

        [Fact]
        public void SpotnetDecoder_HandlesEscapedCharacters()
        {
            // Escaped character: '=j' -> (= followed by byte = original + 42 + 64)
            // Original byte 0x00 escaped -> '=' + (0 + 42 + 64) = '=' + 106 ('j')
            string yencBody = "=j\r\n";
            byte[] inputBytes = Encoding.ASCII.GetBytes(yencBody);
            byte[] outputBuffer = new byte[10];

            var decoder = new SpotnetDecoder();
            uint decodedBytes = decoder.Decode(inputBytes, outputBuffer, 0, (uint)inputBytes.Length);

            Assert.Equal(1u, decodedBytes);
            Assert.Equal(0, outputBuffer[0]);
        }
    }
}
