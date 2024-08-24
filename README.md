# Global Allocator (gAlloc)

`gAlloc` is a custom memory allocator designed to manage heap memory by balancing fragmentation and performance. The heap initially starts at 4 KB, and memory blocks are managed with a header and footer system.

## Allocation

When `gAlloc(nBytes)` is called, the requested number of bytes is aligned to 4-byte boundaries.

- **Large Allocations:**  
  If `nBytes > BLOCK_MAX_PAYLOAD` (32 KB by default), a new block is created directly using `mmap`. This optimization reduces potential fragmentation within the heap, allowing it to be more densely packed.

- **Heap Expansion:**  
  If `nBytes` exceeds the current heap size, the heap is expanded by `nBytes + avg + B_HEAP_RESIZE` (additional average allocation size + 4KB), and the new block is allocated immediately at the `currSize` offset.  
  If `nBytes` is less than the heap size but greater than `currSize`, a new block is also allocated at the end of the heap.

- **Finding a Free Block:**  
  If `nBytes` is smaller than `currSize`, the allocator searches for a free block with a size greater than or equal to `nBytes` by skipping through blocks from offset 0 to `currSize` (`currSize` is the offset of the last block’s footer plus 4, representing the offset for a new block if no free block is found). Once a suitable free block is found, the allocator checks if the block can be split.

### Block Structure

Each block consists of:
- **Header:** 4 bytes
- **Data:** User's allocated memory
- **Footer:** 4 bytes (duplicate of the header)

The header and footer are 4 bytes each, where:
- The first 28 bits represent the size of the data section.
- 2 reserved bits.
- Direction bit (header or footer).
- Free block bit.

### Block Splitting

A free block can be split into two if the remaining space after splitting is at least 12 bytes (4 bytes for the header, 4 bytes for the data, and 4 bytes for the footer). For example, if a free block has 20 bytes of data (28 bytes total), and a 16-byte allocation is requested, splitting would result in a 24-byte block (4 for the header, 16 for the data, and 4 for the footer). Since the remaining 4 bytes would be unusable, the entire 20-byte block is allocated instead.


## Accessing the allocation

Since the `heap` address can change after resizing, any pointers allocated previously would become invalid. To address this, `gAlloc(nBytes)` returns a local offset of the block within the heap, rather than a direct pointer. To access the block, a `access(offset)` function is used.

Keep in mind that for large allocations, `gAlloc(nBytes)` returns a direct pointer to the memory along with an additional 4 bytes, representing the size of the allocation.

## Deallocation

To free a block: 
- Check whether the block is a large object, it was allocated using `mmap` and is freed using `munmap`.

- Retrieve the block's header (address - 4 bytes).
- Otherwise, the `used` bit is set to 0, and adjacent blocks are checked for merging.

### Block Merging

- **Merging with the Previous Block:**  
  If the block above is free (header - 4 bytes), the two blocks are merged by removing the current block's header and the above block's footer, adding 8 bytes of data, and updating the size in the old footer and new header.

- **Merging with the Next Block:**  
  Similar logic applies if the block below is free.

### Heap Shrinking

If the freed block is the last block and its data size exceeds average allocation size + 4 KB, the heap is reduced to match the size, ensuring efficient memory usage.

## Performance

In simple tests with up to 10 allocations/deallocations, `gAlloc` has shown to be 42% faster than directly using `mmap`/`munmap`. The performance gain is expected to vary with different workloads, especially with an increased number of allocated objects.
