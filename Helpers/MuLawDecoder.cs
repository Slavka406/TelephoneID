namespace TelephoneID.Helpers;

public static class MuLawDecoder
{
    public static short MuLawToLinearSample(byte mulaw)
    {
        const int MULAW_BIAS = 0x84;
        mulaw = (byte)~mulaw;

        int sign = (mulaw & 0x80);
        int exponent = (mulaw & 0x70) >> 4;
        int mantissa = mulaw & 0x0F;
        int sample = ((mantissa << 4) + 0x08) << exponent;
        sample -= MULAW_BIAS;

        return (short)(sign != 0 ? -sample : sample);
    }

    public static byte[] Decode(byte[] mulawBytes)
    {
        short[] pcm = new short[mulawBytes.Length];
        for (int i = 0; i < mulawBytes.Length; i++)
        {
            pcm[i] = MuLawToLinearSample(mulawBytes[i]);
        }

        byte[] pcmBytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, pcmBytes, 0, pcmBytes.Length);
        return pcmBytes;
    }
}
