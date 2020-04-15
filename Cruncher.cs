/*
	
	CrunchyKiller - ByteKiller-based C# Port 
	Feb 2020 - github.com/JonathanDotCel
		
	Care has been given to preserve side-by-side assembly vs making use
	of modern language features (so we're using gotos!). Not to everyone's 
	taste, but it's clean and debuggable.
	
	Feel free to document it, but you're probably better off using 
	a newer compression format at this point if compatiblity isn't
	your goal.

	Note: Some branch delay slots have been shuffled around for clarity/parity.
	
	Note: Pushreg, popreg, poptret are stack macros vaguely analogous to X86
		push/pop/ret. I wedged them in aftermarket to clean things up a bit.
	
	Decruncher Credits:
		Based on assembly by Silpheed of Hitmen, in turn based on
		Amiga code by LordBlitter + SurfSmurf
	 
	 Cruncher Credits:
		Based on portable C code by Frank Wille - https://github.com/windenntw
		Note: the header order has been reversed


	
 */
 

using System;
using System.IO;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CheckNamespace
// ReSharper disable once ArrangeTypeModifiers

class CrunchyKiller{
	
	private enum CrunchMode{ NOT_SET, CRUNCH, DECRUNCH, VERIFY }
	private enum CrunchError{ 
		MODE_NOT_SET, CANT_FIND_INPUT, CANT_OPEN_INPUT, 
		CANT_DELETE_EXISTING, CANT_WRITE_OUTPUT,
		VERIFY_FAILED
	}
	
	public const string version = "0.1";
	
	// MIPSL - Little Endian
	static UInt32 read32( byte[] inBytes, int inOffset ){
		
		// read it into an int32 so it shifts properly
		UInt32 returnVal = 0;
		UInt32 readVal = 0;

		readVal = inBytes[ inOffset ];
		returnVal |= readVal;

		readVal = inBytes[ inOffset + 1 ];
		returnVal |= readVal << 8;

		readVal = inBytes[ inOffset + 2 ];
		returnVal |= readVal << 16;

		readVal = inBytes[ inOffset + 3 ];
		returnVal |= readVal << 24;

		return returnVal;

	}


	static void write32( UInt32 inVal, byte[] inBytes, int inOffset ){
		
		// shifting a byte will produce a 32bit int in C#, so cast the whole expression
		inBytes[ inOffset ] =	(byte)( ( inVal & 0xFF ) );
		inBytes[ inOffset + 1 ] = (byte)( ( inVal & 0xFF00 ) >> 8 );
		inBytes[ inOffset + 2 ] = (byte)( ( inVal & 0xFF0000 ) >> 16 );
		inBytes[ inOffset + 3 ] = (byte)( ( inVal & 0xFF000000 ) >> 24 );

	}

	static int Main( string[] args ){
		
		// stack entry point, ORG (where we'll write to) and OEP (original entry point where we jump to)
		bool addEXEHeader = false;

		args = PatchDebugArgs( args );
		
		if ( args.Length != 3 ){
			PrintUsage( false );
			return 1;
		}
		
		CrunchMode mode = CrunchMode.NOT_SET;
		
		string lowerMode = args[0].ToLowerInvariant();

		if ( lowerMode == "-p" || lowerMode == "--p" || lowerMode == "/p" ){
			
			addEXEHeader = true;
			mode = CrunchMode.CRUNCH;

		} else if ( lowerMode == "-c" || lowerMode == "--c" || lowerMode == "/c" ){
			
			mode = CrunchMode.CRUNCH;
			
		} else if ( lowerMode == "-d" || lowerMode == "--d" || lowerMode == "/d" ){ 
			
			mode = CrunchMode.DECRUNCH;
			
		} else if ( lowerMode == "-v" ){
			
			mode = CrunchMode.VERIFY;
			
		}
		
		if ( mode == CrunchMode.NOT_SET ){
			
			return PrintError( CrunchError.MODE_NOT_SET );
			
		}
		
		string inputName = args[1];
		string outputName = args[2];
		
		// Remember the order?
		// use the mnemonic Jump-File, Write-Stack.
		// which is kinda what we're doing.
		// this breaks down as soon as you start questioning it.

		UInt32 headerJumpAddr = 0;
		UInt32 headerFileSize = 0;
		UInt32 headerWriteAddr = 0;		
		UInt32 headerStackAddr = 0;
	
		byte[] readBytes = null;
	
		// Loading a file or generating some random data.
		if ( inputName == "random" ){
		
			readBytes = new byte[ 1000 ];
			
			Random  r = new Random();
			
			int i = 0;
			while( i < readBytes.Length ){
				
				byte bVal = (byte)r.Next();
				readBytes[i] = bVal;
				i++;
				
				// every so often add a sequence of the same byte
				while( (byte)r.Next() < 200 && i < readBytes.Length ){
					readBytes[i] = bVal;
					i++;
				}
				
			}
			
			
		} else {
			
			// Input file exists?
			
			Console.WriteLine( "Opening " + inputName + "..." );

			if ( !File.Exists( inputName ) ){
			
				return PrintError( CrunchError.CANT_FIND_INPUT );
		
			}
	
			// Can we open it?
	
			try{
			
				Console.WriteLine( "Reading " + inputName + "..." );

				readBytes = System.IO.File.ReadAllBytes( inputName );

				if ( addEXEHeader ){
					
					
					headerJumpAddr = read32( readBytes, 0x10 );
					headerFileSize = read32( readBytes, 0x1C );
					headerWriteAddr = read32( readBytes, 0x18 );
					headerStackAddr = read32( readBytes, 0x30 );
					Console.WriteLine( "jumpAddr  : " + headerJumpAddr.ToString( "X8" ) );
					Console.WriteLine( "fileSize  : " + headerFileSize.ToString( "X8" ) );
					Console.WriteLine( "writeAddr : " + headerWriteAddr.ToString( "X8" ) );
					Console.WriteLine( "stackAddr : " + headerStackAddr.ToString( "X8" ) );
					
					// now remove the header from the readbytes!
					// 2048 bytes, 0x800. That's one CD sector for convenience.
					// do you want to dick about with a file stream and potentially introduce new issues...
					// ...or do you want to waste 2mb of RAM for like 2 seconds?
					byte[] newBytes = new byte[ readBytes.Length - 2048 ];

					for( int i = 0; i < newBytes.Length; i++ ){
						newBytes[i] = readBytes[ i + 2048 ];
					}

					// and let the garbage collector do the rest
					readBytes = newBytes;

				}
							

			
			} catch( System.Exception e ){
			
				return PrintError( CrunchError.CANT_OPEN_INPUT, e );
			
			}
			
		} // random vs load
		
		
		// Do we have to delete the output file?
	
		if ( File.Exists( outputName ) ){
		
			// And can we?
		
			try{
		
				File.Delete( outputName );
			
			} catch ( Exception e ){
				
				return PrintError( CrunchError.CANT_DELETE_EXISTING );
				
			}
		
		}
	
		
		byte[] crunchedBytes = null;
		byte[] uncrunchedBytes = null;
	
		if ( mode == CrunchMode.CRUNCH ){
			
			Console.WriteLine( "CRUNCH!" );

			// The juicy bit.
			crunchedBytes = BKCrunch.Crunch( readBytes );
			
			if ( addEXEHeader ){
				
				Console.WriteLine( "Patching in .EXE header..." );

				// if we're compressing a psx .exe, add those 4 32-bit header sections.
				byte[] newBytes = new byte[ crunchedBytes.Length + 0x10 ];
				write32( headerJumpAddr, newBytes, 0x00 );
				write32( headerFileSize, newBytes, 0x04 );
				write32( headerWriteAddr, newBytes, 0x08 );				
				write32( headerStackAddr, newBytes, 0x0C );
				
				// then the rest of it
				for( int i = 0; i < crunchedBytes.Length; i++ ){
					newBytes[ i + 0x10 ] = crunchedBytes[ i ];
				}
				crunchedBytes = newBytes;

			}

		}
		
		if ( mode == CrunchMode.DECRUNCH ){
			
			Console.WriteLine( "Uncrunching " + inputName + "..." );

			// Unsqueeze the juicy bit
			uncrunchedBytes = BKDecrunch.Decrunch( readBytes );
			
		}
		
		if ( mode == CrunchMode.VERIFY ){
			
			// Squeezy unsqueezy
			crunchedBytes = BKCrunch.Crunch( readBytes );
			uncrunchedBytes = BKDecrunch.Decrunch( crunchedBytes );
			
			Console.WriteLine( "Verifying..." );
			
			if ( readBytes.Length != uncrunchedBytes.Length ){
				
				PrintError( CrunchError.VERIFY_FAILED );
				
				Console.WriteLine( 
					"  Size missmatch! Original:" + readBytes.Length + " vs " + uncrunchedBytes.Length 
				);
				return 1;
				
			} else {
				
				for ( int i = 0; i < readBytes.Length; i++ ){
					
					if ( readBytes[i] != uncrunchedBytes[i] ){
						
						PrintError( CrunchError.VERIFY_FAILED );
						
						Console.WriteLine( "  Byte mismatch at offset:" + i.ToString("X8") );
						Console.WriteLine( 
							"  Expecting:" + readBytes[i].ToString("X2") 
											+ "\n Got:" + uncrunchedBytes[i].ToString("X2") 
						);
						return 1;
						
					}
					
				} // for
				
			}
			
			Console.WriteLine( "Files match!" );
			
		} // verify
		
		
		// Write it to disk
		
		try{
		
			// could inline this but it would just be harder to read.
			if( mode == CrunchMode.CRUNCH ){
				File.WriteAllBytes( outputName, crunchedBytes );
			} else {
				File.WriteAllBytes( outputName, uncrunchedBytes );
			}
		
		} catch( Exception e ){
		
			return PrintError( CrunchError.CANT_WRITE_OUTPUT );
			
		}  
		
		// Step3: Profit.
		
		Console.WriteLine( "\n\n...done!" );
		
		return 0;
		
	}
	
	
	
	
	static void PrintUsage( bool justTheTopBit = false ){

		if ( !justTheTopBit ) Console.Clear();
	
		Console.ForegroundColor = ConsoleColor.White;
		Console.Write( " ===================================================\n");
		Console.Write( "  CrunchyKiller - C# ByteKiller port  V" + version + "\n");
		Console.Write( " Feb 2020 - github.com/JonathanDotCel               \n" );
		Console.Write( "\n" );
		Console.Write( "   Thanks to Silpheed, SurfSmurf, LorBlitter,       \n");
		Console.Write( "   Frank Wille, etc.                                \n" );		
		Console.Write( " ===================================================\n");

		if ( justTheTopBit ) return;
	
		Console.ForegroundColor = ConsoleColor.Red;
		Console.Write("\n\n EXAMPLE: \n ");
		Console.ForegroundColor = ConsoleColor.White;
		Console.Write( "crunch -c <INFILE> <OUTFILE>\n");
		Console.Write( "crunch -d <INFILE> <OUTFILE>\n");
		Console.Write( "crunch -p <INDFILE> <OUTFILE>\n" );
		Console.Write( "\n\n" );
		Console.Write( "Note: -p mode is for >= UniROM 8's 16 byte mini header format only." );

	}
	
	static int PrintError( CrunchError inError, System.Exception e = null ){
		
		PrintUsage( false );
		
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine( "\n\n" );
		Console.WriteLine( "Error!" );
		Console.WriteLine( "\n\n" );
		
		switch( inError ){
			
			case CrunchError.MODE_NOT_SET:
				Console.WriteLine( "    Specify crunch or decrunch with -c / -d!" );
				break;
			
			case CrunchError.CANT_FIND_INPUT:
				Console.WriteLine( "    Can't find the input file!" );
				break;
				
			
			case CrunchError.CANT_OPEN_INPUT:
				Console.WriteLine( "    Can't open the input file!" );
				Console.WriteLine( "    ( It may be open in another program? )" );
				Console.WriteLine( "    " + e );
				break;
			
			case CrunchError.CANT_DELETE_EXISTING:
				Console.WriteLine( "    Can't delete existing output file!" );
				Console.WriteLine( "    You have failed me." );
				Console.WriteLine( "    " + e );
				break;
			
			
			case CrunchError.CANT_WRITE_OUTPUT:
			
				Console.WriteLine( "    Can't write the output file!" );
				Console.WriteLine( "    ( It may be open in another program? )" );
				Console.WriteLine( "    " + e );
				
				return 1;
			
			
			
			default:
				throw new ArgumentOutOfRangeException( "inError", inError, null );
			
		}
		
		return 1;
		
	}
	
	
	// Yes it's janky, but yes it's also preferable in every way to dealing
	// with fiddly debug param menus, especially when the VS menu vanishes.
	
	private static string[] PatchDebugArgs( string[] args ){
		
		#if DEBUG
				
		if ( args.Length == 0 ){
			//args = new string[]{ "-c", "c:\\ins\\dosbox\\test.txt", "c:\\ins\\dosbox\\test.bin" };
			//args = new string[]{ "-d", "c:\\ins\\dosbox\\test.bin", "c:\\ins\\dosbox\\test_d.txt" };
			//args = new string[]{ "-v", "c:\\ins\\dosbox\\test.txt", "c:\\ins\\dosbox\\test_verified.txt" };
			//args = new string[]{ "-v", "random", "c:\\ins\\dosbox\\test_verified.txt" };
		}
				
		#endif
		
		return args;
		
	}
	
	
}


// Directly ported from Silpheed's ByteKill.exe / Decrunch.asm
// In turn based on amiga source from LordBlitter & SurfSmurf

static class BKDecrunch{
	
	
	// Let's keep things static to avoid instantiating a second class
	// or passing the bytearray through all the function calls.
	private static byte[] destBytes;
	private static byte[] srcBytes;

	// Since we write backwards, this'll be the finish point.
	// note that it may be offset.
	private static uint decompressAddr = 0;

	// E.g. this point within the array
	private static uint writePointer = 0;

	// The read pointer and its actual value 
	private static uint readPointer = 0;
	private static uint sharedReadVal = 0;
	
	/// <summary>
	/// Decrunch a byte[] array of crunched data.
	/// </summary>
	/// <param name="sourceAddr">
	/// 	Start of compressed data in your byte array.
	/// 	E.g. to test something from a memdump.
	/// 	0 otherwise.
	/// </param>
	/// <param name="destAddr">
	/// 	Start of write area in your byte array.
	///		Usually 0.
	/// </param>
	public static byte[] Decrunch( byte[] inBytes, uint sourceAddr = 0, uint destAddr = 0 ){
	
		srcBytes = inBytes;
		decompressAddr = destAddr;
	
		// Read 32bit uncompressed size from header[0]
		//lw t2,0(a0)					; t2 = uncompressed size
		uint originalSize = Read4Bytes( srcBytes, sourceAddr + 0 );
	
		destBytes = new byte[ originalSize + destAddr ];
	
		Console.WriteLine( "Decrunch: Target Uncompressed FileSize: " + originalSize );
	
		// Read 32bit compressed size from header[4] (includes header)
		//lw t3,4(a0)					; t3 = compressed size
		uint compressedsizeTempT3 = Read4Bytes( srcBytes, sourceAddr + 4 );
		Console.WriteLine( "Decrunch: Compressed Filesize: " + compressedsizeTempT3 );
	
		//addu t2,t2,t1				; t2 = uncompressed size + write address (end of write)
		writePointer = originalSize + decompressAddr;
	
		//addu t0,t0,t3				; t3 = compressed size + read address ( end of read )
		//subiu t0,t0,4				; t0 = end of read address -4;
		readPointer = sourceAddr + compressedsizeTempT3 - 4; 
	

		//lw t3, 0(t0)				; preload first word from t0 ( srcend -4 )
		sharedReadVal = Read4Bytes( srcBytes, readPointer );
	
		BK_MainLoop();
	
		return destBytes;
	
	}


	static void BK_MainLoop(){
	
		// Yep, we're in a while loop.
		BK_mainloop:
	
		//jal BK_getnextbit
		//nop
		uint NEXTBIT_V0 = BK_getnextbit();
	
		//beq zero,v0,@BK_part2
		//nop
		if ( NEXTBIT_V0 == 0 ) goto BK_part2;
		
		//li a0, 2
		//jal BK_readbits
		//nop
		uint firstReadBits = BK_ReadBits( 2 );
        
		//slti t4, v0, 2
		uint lessThan2 = ( firstReadBits < 2 ) ? (uint)1 : (uint)0;
			
		//beq zero,t4,@BK_skip1
		//nop
		// Time to end.
		if ( lessThan2 == 0 ) goto BK_skip1;
			
		//addiu a0,BITSREAD,9
		uint arg0 = firstReadBits + 9;
			
		//addiu a1,BITSREAD,2
		uint arg1 = firstReadBits + 2;

		//jal BK_dodupl
		//nop
		BK_DoDupl( arg0, arg1 );
				
		goto BK_endloop; 
		//b @BK_endloop
		//nop
			
	
		BK_skip1:
		
		uint SUBVAL_T4 = firstReadBits - 3;
		
		if ( SUBVAL_T4 != 0 ) goto BK_skip2;
		
		BK_DoJMP( 8, 8 );
		
		goto BK_endloop;
	
		BK_skip2:
		
		uint morebits = BK_ReadBits( 8 );
		
		BK_DoDupl( 12, morebits );
		
		goto BK_endloop;
		
		
		BK_part2:
	
		uint bittybit = BK_getnextbit();
		
		if ( bittybit == 0 ) goto BK_skip3;
		
		BK_DoDupl( 8, 1 );
		
		goto BK_endloop;
	
		BK_skip3:
		
		BK_DoJMP( 3, 0 );
	
		BK_endloop:
	
		if ( writePointer != decompressAddr ) goto BK_mainloop;
	
	
	}



	static uint BK_getnextbit(){
	
		//andi v0,t3,1
		uint RETURNVAL_V0 = sharedReadVal & 1;

		//srl t3,t3,1
		sharedReadVal = sharedReadVal >> 1;

		//bne zero,t3,@BK_gnbend
		//nop
		if ( sharedReadVal == 0 ){
		
			// read in the next byte if we have to
			//subiu t0,t0,4
			readPointer -= 4;
		
			//lw t3, (t0)
			sharedReadVal = Read4Bytes( srcBytes, readPointer );
		
			// But also grab the last bit and shuffle by one
			//andi v0,t3,1
			RETURNVAL_V0 = sharedReadVal & 1;
			
			//srl t3,t3,1
			sharedReadVal = sharedReadVal >> 1;

			//lui t5, $8000
			//or t3,t3,t5
			uint WTF_T5 = 0x80000000;
			sharedReadVal = sharedReadVal | WTF_T5;
		
		
		}
                
		//@BK_gnbend:
		//popret ra
		//nop
		return RETURNVAL_V0;
	}



	static uint BK_ReadBits( uint inBitCount ){
	
		//pushreg ra
	
		//move v1, zero
		uint returnVal_V1 = 0;
		uint bitCount = inBitCount;

		//@BK_rbloop
		//beq zero,a0,@BK_rbend
		//nop
		while ( bitCount != 0 ){
				
			//subiu a0,a0,1
			bitCount --;
				
			//sll v1,v1,1
			returnVal_V1 = returnVal_V1 << 1;

			//jal BK_getnextbit
			//nop
			uint NEXTBIT = BK_getnextbit();
				
			//or v1,v1,v0
			returnVal_V1 |= NEXTBIT;
		
			//b @BK_rbloop
			//nop
			//@BK_rbend
		}
			
	
		//move v0,v1
		//popret ra
		//nop
		return returnVal_V1;
	
	}


	static void BK_DoJMP( uint arg0, uint arg1 ){
	
		//pushreg ra
		//nop
			
		//jal BK_readbits
		//nop
		uint readbits_V0 = BK_ReadBits( arg0 );
	
		//addu t4,v0,a1
		//addiu t4,t4,1
		//nop
		uint TEMP_T4 = readbits_V0 + arg1;
		TEMP_T4 += 1;
	
		//@BK_djloop
		//beq zero,t4,@BK_djend
		//nop
		while( TEMP_T4 != 0 ){
		
			//subiu t4,t4,1
			TEMP_T4 -= 1;
		
		
			// subiu t2,t2,1
			writePointer -= 1;
		
			// li a0, 8
			//jal BK_readbits
			//nop
			readbits_V0 = BK_ReadBits( 8 );
		
			//sb v0, (t2)
			//nop
			destBytes[ writePointer ] = (byte)readbits_V0;
		
		}
	
	
	}

	static void BK_DoDupl( uint arg0, uint arg1 ){
			
		//pushreg ra
	
		// thankfully arg a1 isn't used as a param for a sub call
		//addiu a1,a1,1
		uint counter = arg1 + 1;
	
		//jal BK_readbits
		//nop
		// a0 just passed right through
		uint readBits_V0 = BK_ReadBits( arg0 );
	
		//@BK_ddloop
		//beq zero,a1,@BK_ddend
		//nop
		while ( counter != 0 ){
            
			//subiu a1,a1,1
			counter -= 1;
		
			//subiu t2,t2,1
			writePointer -= 1;
		
			//addu t4,t2,v0
			uint temp_T4 = readBits_V0 + writePointer; 
		
			// Definitely write bytes.. storing something?
			temp_T4 = destBytes[ temp_T4 ];
		
			destBytes[ writePointer ] = (byte) temp_T4;
		
			if ( srcBytes.Length < 100 )
				Console.WriteLine( "Wrote " + ((char)temp_T4).ToString() );
		
			//@BK_ddend
		}
	
		//popret ra
		return;
	
	}

	// Let's not attempt to use ambiguous "WORD"/"DWORD" etc. Call it what it is.
	// NOTE: Little Endian on X86 and MIPS
	static uint Read4Bytes( byte[] sourceData, uint startByteOffset ){
	
		// you can have it pretty, or you can have it readable in the debugger...
		uint a = sourceData[ startByteOffset ];
		uint b = ( (uint)sourceData[ startByteOffset +1 ] << 8 );
		uint c = ( (uint)sourceData[ startByteOffset + 2 ] << 16 );
		uint d = ( (uint)sourceData[ startByteOffset + 3 ] << 24 ); 
	
		// so much casting.
		uint returnVal = (uint)( a | b | c | d );
	
		return returnVal;
	
	}



}



// Based on portable C code by Frank Wille ( See header )
// Concessions have been made to have it closely follow the original code
// vs using more modern procedures, conventions and features.

static class BKCrunch{
	
	static readonly uint[] MAX_OFFSETS = { 0x100, 0x200, 0x400, 0x1000 };
	static readonly uint[] OFFS_BITS = { 8, 9, 10, 12 };
	static readonly uint[] CMD_BITS = { 2, 3, 3, 3 };
	static readonly uint[] CMD_WORDS = { 1, 4, 5, 6 };
	
	// 4096 doesn't seem to play nicely with the PSX source.
	static uint scanWidth = 2048;
	
	private static byte[] destBytes;
	
	// think of it as the tape head on the source
	private static uint srcPointer = 0;
	
	// tape head for the destination buffer
	static uint dstPointer;
	
	// How many free bytes in the destination buffer
	static uint dstFreeBytes;
	
	// Little bit buffer that's written out when 
	// it fills to 32 bit
	static uint bitstream = 0;
	static uint bitsfree = 32;
	
	static uint chksum = 0;
	

	static void Write32_LittleEndian( uint offset, uint value ){
		
		// DBG
		// string dHex = value.ToString( "X2" );
		// Console.WriteLine( "writing " + value + " | " + dHex );
		
		destBytes[ offset ] = (byte)(value & 0xff);
		destBytes[ offset +1 ] = (byte)( (value >> 8) & 0xFF );
		destBytes[ offset +2 ] = (byte)( (value >> 16) & 0xFF );
		destBytes[ offset +3 ] = (byte)( (value >> 24) & 0xFF );
		
	}
	
	
	static void writeLong(){
		
		if ( dstFreeBytes < 4 ){
			
			// Double the destination buffer as a List<T> would
			byte[] newDest = new byte[ destBytes.Length + dstPointer ];
			for( int i = 0; i < destBytes.Length; i++ ){
				newDest[i] = destBytes[i];
			}
			
			destBytes = newDest;
			
			// praise to the GC()
			
			dstFreeBytes = (uint)( destBytes.Length - dstPointer ); 
			
			System.Console.WriteLine( "Adding some bytes. Free: " + dstFreeBytes );
			
		}
		
		Write32_LittleEndian( dstPointer, bitstream );
		dstPointer += 4;
		dstFreeBytes -= 4;
		
		chksum ^= bitstream;
		
		bitstream = 0;
		bitsfree = 32;
		
	}
	
	static void WriteBits( uint numBits, uint inValue ){
		
		while( numBits != 0 ){
			
			bitstream = ( bitstream << 1 ) | ( inValue & 1 );
			inValue >>= 1;
			
			bitsfree--;
			
			if ( bitsfree == 0 ){
				writeLong();
			}
			
			numBits--;
			
		}
		
	}
	
	static void Dump( uint inVal ){
		
		if ( inVal >= 9 ){
			
			WriteBits( 11, 0x700 | ( inVal - 9 ) );
			
		} else if ( inVal >= 1 ){
			
			WriteBits( 5, inVal -1 );
			
		}
		
		
	}
	
	
	public static byte[] Crunch( byte[] inBytes ){
		
		destBytes = new byte[ inBytes.Length ];
		
		Console.WriteLine( "Crunch: loaded filesize: " + inBytes.Length );
		
		srcPointer = 0;
		
		chksum = 0;
		uint dmpcnt = 0;
		
		uint scanEnd = 0;
		uint scanPtr = 0;
		
		// Current copy vars vs best we've found on this pass
		uint copyLen = 0;
		uint copyOff = 0;
		uint copyType = 0;
		
		uint bestOffs = 0;
		uint bestLen = 1;
		uint bestType = 0;
		
		dstPointer = 12;
		dstFreeBytes = (uint)destBytes.Length - dstPointer;
		
		while( srcPointer < inBytes.Length ){
			
			scanEnd = srcPointer + scanWidth;
			
			if ( scanEnd >= inBytes.Length )
				scanEnd = (uint)inBytes.Length - 1;
			
			bestLen = 1;
			scanPtr = srcPointer + 1;
			
			// scan for sequences to copy
			while( scanPtr < scanEnd ){
				
				// Do you want it pretty, or do you want it debuggable?
				byte a = inBytes[ srcPointer ];
				byte b = inBytes[ scanPtr ];
				byte c = inBytes[ srcPointer + 1 ];
				byte d = inBytes[ scanPtr + 1 ];
				if( 
					a == b && c == d
				){
					
					// can copy at least 2 bytes, determine length of sequence
					for( 
						copyLen = 0;
						scanPtr + copyLen < scanEnd && inBytes[ srcPointer + copyLen ] == inBytes[ scanPtr + copyLen ];
						copyLen++
					){
						// The for loop has no body. Just noting my appreciation.
					}
					
					// Have we found a longer sequence than the current best?
					if ( copyLen > bestLen ){
						
						// found new longest seq to copy
						copyOff = scanPtr - srcPointer;
						if ( copyLen > 4 ){
							
							copyType = 3;
							// max seq length = 256
							if ( copyLen > 0x100 )
								copyLen = 0x100;
								
						} else {
							
							copyType = copyLen - 2;
							
						}
						
						if ( copyOff < MAX_OFFSETS[ copyType ] ){
							
							// remember the new best sequence
							bestLen = copyLen;
							bestOffs = copyOff;
							bestType = copyType;
							
						}
						
					} // copylen > bestLen
					
					scanPtr += copyLen;
					
				} else {
					
					// No sequence, just advance by a byte.
					scanPtr++;
					
				} // 2 bytes match
				
			} // scanPtr < scanEnd
			
			// DBG
			// char readByte = (char)srcBytes[ srcPointer ];
			// Console.WriteLine( " CurrentChar " + (char)readByte );
			
			if ( bestLen > 1 ){
			
				// we found a little sequence
				
				Dump( dmpcnt );
				dmpcnt = 0;
				
				WriteBits( OFFS_BITS[ bestType ], bestOffs );
				
				if ( bestType == 3 ){
					WriteBits( 8, bestLen -1 );
				}
				
				WriteBits( CMD_BITS[ bestType ], CMD_WORDS[ bestType ]);
				
				srcPointer += bestLen;
				
			} else {
				
				// dump it as-is
				
				WriteBits( 8, 
				inBytes[ srcPointer ] );
				
				srcPointer++;
				
				
				if ( ++dmpcnt >= 0x108 ){
					Dump( dmpcnt );
					dmpcnt = 0;
				}
				 
				
			} // !bestLen > 1
			
		} //srcPointer < srcLen
		
		
		// Write out whatever's left in the byte buffer
		
		Dump( dmpcnt );
		
		bitstream |=   (uint)(  (long)1 << (int)( 32 - bitsfree )  );
		writeLong();
		
		// Write uncompressed size to header + 0
		Write32_LittleEndian( 0, (uint)inBytes.Length );
		
		// Write compressed size to header + 4
		Write32_LittleEndian( 4, dstPointer  );
		
		// Write the checksum to header + 8;
		Write32_LittleEndian( 8, chksum );
		
		Console.WriteLine( "Crunch: compressed size: " + dstPointer );
		
		// Prune the dest bytes to the right size
		
		byte[] newBytes = new byte[ dstPointer ];
		
		for( int i = 0; i < newBytes.Length; i++ ){
			newBytes[ i ] = destBytes[ i ];
		}
		destBytes = newBytes;
		
		return destBytes;
		
	}  
	
	
}






