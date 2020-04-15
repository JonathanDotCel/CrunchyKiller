; ByteKiller decrunch code for PSX by Silpheed of Hitmen
;
; Added some documentation and stack macros, Jun 2014, Jonathan Cel
; Note: this has been included in the CrunchyKiller source purely
:   as a reference for the C# port. Please see the Hitemen PSX
:   website for the original downloads!

// TODO: github

; BK_Decrunch
; a0 - src
; a1 - dest

;======================================================
; Decrunch - Omnomnom
;======================================================


BK_Decrunch
				
                ;move t9, ra
                pushreg ra
                nop
                
                move t0, a0					; t0 = source address
                nop
                
                lw t2,0(a0)					; t2 = uncompressed size
                move t1, a1					; t1 = write address
                lw t3,4(a0)					; t3 = compressed size
                addu t2,t2,t1				; t2 = uncompressed size + write address (end of write)
                addu t0,t0,t3				; t3 = compressed size + read address ( end of read )
                subiu t0,t0,4				; t0 = end of read address -4;
                lw t3, 0(t0)				; preload first word from t0 ( srcend -4 )
                nop

;======================================================
; BK_mainloop
;======================================================
				
BK_mainloop
				
				jal BK_getnextbit
                nop
                
                beq zero,v0,@BK_part2
                nop
                
				li a0, 2
                jal BK_readbits
                nop
                
                slti t4, v0, 2
                beq zero,t4,@BK_skip1
                nop
				
                addiu a0,v0,9
				addiu a1,v0,2
                jal BK_dodupl
                nop
                
                b @BK_endloop
                nop
                
@BK_skip1:
				subiu t4,v0,3
                bne zero,t4,@BK_skip2
                nop
				
                li a0, 8
				li a1, 8
                jal BK_dojmp
                nop
                
                b @BK_endloop
                nop
                
@BK_skip2:
				jal BK_readbits
                li a0, 8
                
                move a1,v0
				li a0, 12
                jal BK_dodupl
                nop
                
                b @BK_endloop
                nop
@BK_part2:
                jal BK_getnextbit
                nop
				
                beq zero,v0,@BK_skip3
                nop

					li a0, 8
					li a1, 1
					jal BK_dodupl
					nop
                
					b @BK_endloop
					nop
                
@BK_skip3:
				li a0, 3
				move a1, zero
                jal BK_dojmp
				nop
                
				
@BK_endloop:
				bne t2,t1,BK_mainloop
                nop
                
				;move ra, t9
                ;jr ra
                ;nop
				popret ra
				nop

;======================================================
; BK_getnextbit
;======================================================

BK_getnextbit:
				
				pushreg ra
				
				andi v0,t3,1
                srl t3,t3,1
                bne zero,t3,@BK_gnbend
                nop
                
                subiu t0,t0,4
                lw t3, (t0)
                nop
                andi v0,t3,1
                srl t3,t3,1
                lui t5, $8000
                or t3,t3,t5
                
@BK_gnbend:
				;jr ra
                ;nop
				popret ra
				nop

;======================================================
; BK_readbits
;======================================================

BK_readbits     
				
				pushreg ra
				
				move v1, zero
                move t8,ra
@BK_rbloop
				beq zero,a0,@BK_rbend
                nop
                
                subiu a0,a0,1
                
				sll v1,v1,1
                jal BK_getnextbit
                nop
                or v1,v1,v0
                
                b @BK_rbloop
                nop
                
@BK_rbend
				move v0,v1
				move ra,t8
                ;jr ra
                ;nop
				popret ra
				nop
                
;======================================================
; BK_dojmp
;======================================================
			
BK_dojmp
				;move t7, ra
				pushreg ra
				nop
				
                jal BK_readbits
                nop
                
                addu t4,v0,a1
                addiu t4,t4,1
				nop
				
@BK_djloop
				beq zero,t4,@BK_djend
                nop
				
                subiu t4,t4,1
				
                li a0, 8
				subiu t2,t2,1
				nop
                jal BK_readbits
                nop
                
                sb v0, (t2)
                nop
                
                b @BK_djloop
                nop
                
@BK_djend
				;move ra, t7
				;jr ra
                ;nop
				popret ra

;======================================================
; BK_dodupl
;======================================================

BK_dodupl
				
				pushreg ra
				
				move t7, ra
				addiu a1,a1,1
				
                jal BK_readbits
                nop
				
@BK_ddloop
				beq zero,a1,@BK_ddend
                nop
                
                subiu a1,a1,1
                
                subiu t2,t2,1
                addu t4,t2,v0
                lb t4, (t4)
                nop
                sb t4, (t2)
                nop
				
                b @BK_ddloop
                nop
				
@BK_ddend
				popret ra
				
				;move ra, t7
				;jr ra
                ;nop
				