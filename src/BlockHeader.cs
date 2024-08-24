using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Size = 4)]
[Serializable]
public unsafe struct BlockHeader
{
    // The payload consists of block length, 2 reserved bits, direction bit, and a usage bit.
    public int Payload;

    public int GetPayloadSize() => Payload >> 4;
    public void SetPayloadSize(int size) => Payload = size << 4;
    public void SetUsedBit(bool b) => Payload = b ? Payload | 0b1 : Payload & ~0b1;
    public void SetDirectionBit(bool down) => Payload = down ? Payload | 0b10 : Payload & ~0b10;
    public int Size => GetPayloadSize() + 2*sizeof(BlockHeader);
    public bool IsUsed => (Payload & 0b1) > 0;
    public bool IsUpperHeader => (Payload & 0b10) > 0;

}