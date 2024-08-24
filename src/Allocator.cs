using System.Linq.Expressions;
using static LinuxHelper;
namespace GAlloc;

public unsafe class Allocator : IDisposable
{
    public const int HEAP_MIN_SIZE = 1 << 12;
    public const int HEAP_MAX_SIZE = 1 << 16;
    public const int B_HEAP_RESIZE = 1 << 12;
    public const int BLOCK_MIN_PAYLOAD = 4;
    public const int BLOCK_MAX_PAYLOAD = 1 << 15;
    private IntPtr heap;
    private int size = HEAP_MIN_SIZE;
    private int currOffset;


    private long allocSum = 0;
    private int allocCount = 0;
    public Allocator()
    {
        heap = mmap(IntPtr.Zero, HEAP_MIN_SIZE, LinuxProt.PROT_READ | LinuxProt.PROT_WRITE, LinuxFlags.MAP_ANONYMOUS | LinuxFlags.MAP_PRIVATE, -1, 0);
    }
    /// <summary>
    /// Returns an offset within internal dynamic heap, to actually receive the allocation invoke access() method
    /// </summary>
    /// <param name="nBytes"></param>
    /// <returns></returns>
    public IntPtr gAlloc(int nBytes)
    {
        nBytes = nBytes + 3 & ~3;

        if (nBytes >= BLOCK_MAX_PAYLOAD)
        {
            // just mapping the object into the process virtual memory, adding a checksum to identify large object
            IntPtr largeObject = mmap(IntPtr.Zero, nBytes + 4, LinuxProt.PROT_READ | LinuxProt.PROT_WRITE,
                LinuxFlags.MAP_PRIVATE | LinuxFlags.MAP_ANONYMOUS, -1, 0);
            *(int*)largeObject = nBytes;

            return largeObject + 4;
        }

        allocCount++;
        allocSum += nBytes;

        if (nBytes < currOffset - 2 * sizeof(BlockHeader))
        {
            nint res = firstFit(nBytes);
            if (res != IntPtr.Zero) return getLocalOffset(res);
        }

        if (nBytes + currOffset > size)
        {
            if (size >= HEAP_MAX_SIZE) {
                allocCount--;
                allocSum -= nBytes;
                return -1;
            }

            resize(nBytes + B_HEAP_RESIZE + getAvgAllocSize());
        }

        BlockHeader* newBlock = createBlock(nBytes, heap + currOffset);
        currOffset += newBlock->Size;
        return getLocalOffset((IntPtr)newBlock);
    }
    public IntPtr access(IntPtr positiveOffset) 
    {
        if (positiveOffset < 0 || positiveOffset > currOffset) // large object
            return positiveOffset;
        BlockHeader* header = (BlockHeader*)(heap + positiveOffset);

        if (!header->IsUsed || header->GetPayloadSize() < BLOCK_MIN_PAYLOAD) 
        {
            return IntPtr.Zero;
        }

        return (IntPtr)(header + 1);
    }
    public bool free(IntPtr positiveOffset)
    {
        if (positiveOffset < 0 || positiveOffset > currOffset) // large obj      
            return munmap(positiveOffset - 4, *(int*)(positiveOffset - 4)) == 0;

        BlockHeader* header = (BlockHeader*)(heap + positiveOffset);

        BlockHeader* bottomHeader = (BlockHeader*)((IntPtr)header + header->GetPayloadSize() + sizeof(BlockHeader));
        if (!header->IsUsed || !bottomHeader->IsUsed)
            return false;

        header->SetUsedBit(false);
        bottomHeader->SetUsedBit(false);

        allocCount--;
        allocSum -= header->GetPayloadSize();
        
        if (positiveOffset - sizeof(BlockHeader) > sizeof(BlockHeader) && !(header - 1)->IsUsed)
        {
            bottomHeader = mergeFree(header, header - 1);
        }
        if (getLocalOffset((IntPtr)(bottomHeader + 1)) < currOffset && !(bottomHeader + 1)->IsUsed)
        {
            bottomHeader = mergeFree(bottomHeader + 1, bottomHeader);
        }
        

        if (getLocalOffset((IntPtr)(bottomHeader + 1)) >= currOffset && bottomHeader->GetPayloadSize() > B_HEAP_RESIZE + getAvgAllocSize())
        {
            currOffset -= 2 * sizeof(BlockHeader) + bottomHeader->GetPayloadSize();
            resize(-(bottomHeader->GetPayloadSize() - B_HEAP_RESIZE - getAvgAllocSize()));
            *(int*)(heap + currOffset) = 0;
        }
        return true;
    }
    private BlockHeader* mergeFree(BlockHeader* from, BlockHeader* into)
    {
        int newPayloadSize = into->GetPayloadSize() + from->GetPayloadSize() + 2 * sizeof(BlockHeader);
        BlockHeader* bottomHeader;
        if (from->IsUpperHeader)
        {
            (into - into->GetPayloadSize() / sizeof(BlockHeader) - 1)->SetPayloadSize(newPayloadSize);
            (bottomHeader = from + 1 + from->GetPayloadSize() / sizeof(BlockHeader))->SetPayloadSize(newPayloadSize);
        }
        else
        {
            (bottomHeader = into + into->GetPayloadSize() / sizeof(BlockHeader) + 1)->SetPayloadSize(newPayloadSize);
            (from - from->GetPayloadSize() / sizeof(BlockHeader) - 1)->SetPayloadSize(newPayloadSize);
        }
        *(int*)from = *(int*)into = 0;

        return bottomHeader;
    }
    private void resize(int nAdditional)
    {
        heap = mremap(heap, size, size + nAdditional, LinuxFlags.MREMAP_MAYMOVE, IntPtr.Zero);
        size += nAdditional;
    }
    private IntPtr firstFit(int nBytes)
    {
        BlockHeader* upperHeader = (BlockHeader*)heap;
        while (upperHeader->IsUsed || upperHeader->GetPayloadSize() < nBytes)
        {
            if (getLocalOffset((IntPtr)upperHeader) >= currOffset) // got to the end of allocated blocks
                return IntPtr.Zero;

            upperHeader += upperHeader->Size / sizeof(BlockHeader);
        }

        upperHeader->SetUsedBit(true);


        // checking whether it is possible to divide current freed block into 2 blocks
        if (upperHeader->Size - nBytes - 4 * sizeof(BlockHeader) >= BLOCK_MIN_PAYLOAD)
        {
            createBlock(nBytes, (IntPtr)upperHeader);
            createBlock(upperHeader->Size - nBytes - 4 * sizeof(BlockHeader), (IntPtr)upperHeader + nBytes + 2 * sizeof(BlockHeader));
        }
        return (IntPtr)upperHeader;
    }
    private BlockHeader* createBlock(int dataLength, IntPtr addr)
    {
        BlockHeader* upperHeader = (BlockHeader*)addr;
        upperHeader->SetPayloadSize(dataLength);
        upperHeader->SetDirectionBit(down: true);
        upperHeader->SetUsedBit(true);

        BlockHeader* bottomHeader = (BlockHeader*)(addr + sizeof(BlockHeader) + dataLength);
        bottomHeader->SetPayloadSize(dataLength);
        bottomHeader->SetDirectionBit(down: false);
        bottomHeader->SetUsedBit(true);

        return upperHeader;
    }
    private IntPtr getLocalOffset(IntPtr offset) => Math.Abs(offset) - Math.Abs(heap);
    private int getAvgAllocSize() => (int)(allocSum / allocCount) + 3 & ~3;
    public void Dispose()
    {
        munmap(heap, size);
    }
}
