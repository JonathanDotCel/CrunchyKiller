/*
 * ByteKiller Cruncher - Little Endian for PSX
 *
 * Very slight modifications, Feb 2020, Jonathan Cel
 *
 * - Swapped header arrangement to [Original Size], [Compressed Size], [Checksum]
 * - Documentation
 * - Switched the write function to Little Endian to match MIPS / X86 archs
 * - Rearranged a few --var and *var++ calls to simplify further conversion 
 * -     to languages not supporting pointers / those features
 * - Refactor to C# conventions out of personal preference
 * - Clamped the SCANWIDTH (now const) to 2048.
 * 
 * ByteKiller Cruncher, Original Portalbe C code:
 * Frank Wille <frank@phoenix.owl.de> 2012 - https://github.com/windenntw
 * 
 * Further based on Amiga source by LordBlitter and SurfSmurf
 *
 */
 // TODO: github

#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>

static const uint32_t MAX_OFFSETS[ 4 ] = { 0x100, 0x200, 0x400, 0x1000 };
static const int OFFSET_BITS[ 4 ] = { 8, 9, 10, 12 };
static const int CMD_BITS[ 4 ] = { 2, 3, 3, 3 };
static const uint32_t CMD_WORD[ 4 ] = { 1, 4, 5, 6 };


// static uint32_t ScanWidth = 4096;
// Limiting to 2048 for compatibility
static const uint32_t SCANWIDTH = 2048;

static uint8_t *dst, *dststart;
static size_t dstFreeBytes;

static uint32_t chksum;

// Like the tape head, current value and remaining bits.
static uint32_t bitStream;
static int bitsFree;


// MIPS, X86
static void Write32_LittleEndian( uint8_t *p, uint32_t v )
{

	*p++ = v & 0xff;
	*p++ = ( v >> 8 ) & 0xff;
	*p++ = ( v >> 16 ) & 0xff;
	*p = v >> 24;

}

static void Write32_BigEndian( uint8_t *p, uint32_t v )
{

	*p++ = v >> 24;
	*p++ = ( v >> 16 ) & 0xff;
	*p++ = ( v >> 8 ) & 0xff;
	*p = v & 0xff;

}


static void WriteLong( void )
{

	// Running out of destination buffer space?
	// realloc!
	if ( dstFreeBytes < 4 ) {

		uint32_t off = dst - dststart;

		dststart = realloc( dststart, off * 2 );
		dst = dststart + off;
		dstFreeBytes = off;
	}

	Write32_LittleEndian( dst, bitStream );
	dst += 4;
	dstFreeBytes -= 4;
	chksum ^= bitStream;

	bitStream = 0;
	bitsFree = 32;

}


static void WriteBits( int numBits, uint32_t inValue )
{

	while ( numBits != 0 ) {

		bitStream = ( bitStream << 1 ) | ( inValue & 1 );

		inValue >>= 1;

		bitsFree--;

		if ( bitsFree == 0 ){

			WriteLong();

		}

		numBits--;

	}

}


static void Dump( int n )
{
	if ( n >= 9 )
		WriteBits( 11, 0x700 | ( n - 9 ) );
	else if ( n >= 1 )
		WriteBits( 5, n - 1 );
	
}



int bk_crunch( uint8_t *src, size_t srclen, void **buf, size_t *buflen )
{
	uint8_t *srcend;
	uint8_t *scanptr, *scanend;
	int dumpCount;

	// See below
	int copyLen = 0;
	int copyType = 0;
	uint32_t copyOffs = 0;
	
	// The best sequence we found during the scan
	int bestLen = 0;
	int bestType = 0;	
	uint32_t bestOffs = 0;

	
	srcend = src + srclen;
	dststart = malloc( srclen / 4 + 12 );
	dst = dststart + 12;
	dstFreeBytes = srclen / 4;

	bitStream = 0;
	bitsFree = 32;
	chksum = 0;
	dumpCount = 0;

	// see usage for decription
	// uint32_t* originalStart = src;

	while ( src < srcend ) {

		scanend = src + SCANWIDTH;
		if ( scanend >= srcend )
			scanend = srcend - 1;

		bestLen = 1;
		scanptr = src + 1;

		/* scan for sequences to copy */
		while ( scanptr < scanend ) {

			// Do you want it pretty, or do you want it easy to debug?
			uint8_t a = src[ 0 ];
			uint8_t b = scanptr[ 0 ];
			uint8_t c = src[ 1 ];
			uint8_t d = scanptr[ 1 ];

			if ( a == b && c == d ) {

				/* can copy at least 2 bytes, determine length of sequence */
				for ( copyLen = 0;
					&scanptr[ copyLen ] < scanend && src[ copyLen ] == scanptr[ copyLen ];
					copyLen++ );



				if ( copyLen > bestLen ) {

					/* found new longest sequence to copy */
					copyOffs = scanptr - src;

					if ( copyLen > 4 ) {
						copyType = 3;
						if ( copyLen > 0x100 )
							copyLen = 0x100;  /* max sequence length is 256 */
					} else{
						copyType = copyLen - 2;
					}

					if ( copyOffs < MAX_OFFSETS[ copyType ] ) {
						/* remember new best sequence */
						bestLen = copyLen;
						bestOffs = copyOffs;
						bestType = copyType;
					}

				}

				scanptr += copyLen;

			} else {
				scanptr++;
			}

		} // scanPtr < scanEnd

		// Since VS's watches are so limited in static contexts:
		/*
		int blah = src - originalStart;
		char bubbles = src[ 0 ];
		fprintf( stdout, "Char %c ", bubbles );
		*/

		if ( bestLen > 1 ) {

			/* we found a copy-sequence */
			Dump( dumpCount );
			dumpCount = 0;

			WriteBits( OFFSET_BITS[ bestType ], bestOffs );

			if ( bestType == 3 ){
				WriteBits( 8, bestLen - 1 );
			}

			WriteBits( CMD_BITS[ bestType ], CMD_WORD[ bestType ] );

			src += bestLen;

		} else {

			/* nothing to copy, dump the current src-byte */
			WriteBits( 8, *src );
			src++;

			dumpCount++;
			if ( dumpCount >= 0x108 ) {

				Dump( dumpCount );
				dumpCount = 0;

			}

		} // !bestLen > 1

	} //srcPointer < srcLen

	// Write whatever's left in the buffer:
	Dump( dumpCount );
	bitStream |= 1L << ( 32 - bitsFree );
	WriteLong();


	//[ header + 0 ] = uncompressed size	
	Write32_LittleEndian( dststart, srclen );

	// [ header + 4 ] = compressed size ( including header )	
	Write32_LittleEndian( dststart + 4, dst - dststart );

	// [ header + 8 ] = checksum
	Write32_LittleEndian( dststart + 8, chksum );

	*buf = dststart;
	*buflen = dst - dststart;

	return 0;

}

int main( int argc, char *argv[] ){

	CrunchMain( argc, argv );

}

// Temporary patch until VS2017's debug property panel ( and thus
// command line params for debug mode ) is fixed.
int altMain( int argc, char *argv[] ){

	char* testValues[] = { "", "test.txt", "text.bin" };
	CrunchMain( 4, testValues );

}

int CrunchMain( int argc, char *argv[] )
{
	int rc = 1;

	// bytekiller.exe inFile outFile

	if ( argc == 3 ) {

		FILE *in, *out;
		void *p, *buf;
		size_t flen, buflen;

		if ( in = fopen( argv[ 1 ], "rb" ) ) {

			// Get the length by seeking to the end
			fseek( in, 0, SEEK_END );
			flen = ftell( in );
			fseek( in, 0, SEEK_SET );

			// alloc that many bytes and read it
			if ( p = malloc( flen ) ) {

				if ( fread( p, 1, flen, in ) == flen ) {

					// Not any more
					/*
					if (argc == 4) {

					  ScanWidth = atoi(argv[3]);
					  if (ScanWidth < 8)
						ScanWidth = 8;
					  else if (ScanWidth > 4096)
						ScanWidth = 4096;

					}
					*/

					// Crunch!
					if ( bk_crunch( p, flen, &buf, &buflen ) == 0 ) {

						if ( out = fopen( argv[ 2 ], "wb" ) ) {
							fwrite( buf, 1, buflen, out );
							fclose( out );
							rc = 0;
						} else
							fprintf( stderr, "Cannot open output file '%s'!\n", argv[ 2 ] );
					} else
						fprintf( stderr, "Cannot crunch '%s'.\n", argv[ 1 ] );
				} else
					fprintf( stderr, "Read error on '%s'!\n", argv[ 1 ] );
			} else
				fprintf( stderr, "Failed to allocate %lu bytes of memory!\n", flen );
			fclose( in );
		} else
			fprintf( stderr, "Cannot open input file '%s'!\n", argv[ 1 ] );
	} else
		fprintf( stderr, "Usage: %s <src file> <crunched file> [scan width]\n",
			argv[ 0 ] );


	//fflush( stdout );
	//getchar();

	return rc;
}
