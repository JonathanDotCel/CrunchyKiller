; ByteKiller decrunch code for PSX by Silpheed of Hitmen

; BK_Decrunch
; a0 - src
; a1 - dest

;======================================================
; Decrunch - Omnomnom
;======================================================

BK_Decrunch
				
				pushreg ra
				nop
				
                move t0, a0
				nop
				
                lw t2,0(a0)
                move t1, a1
                lw t3,4(a0)
                addu t2,t2,t1
                addu t0,t0,t3
                subiu t0,t0,4
                lw t3, 0(t0)
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
				li a0, 8
				jal BK_readbits
                nop
                
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
				
				popret ra
				nop

;======================================================
; BK_readbits	a0 = numBits
;======================================================

BK_readbits     
				
				pushreg ra
				
				move v1, zero
                

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

				
				popret ra
				nop
                
;======================================================
; BK_dojmp    arg0    arg1
;======================================================
			
BK_dojmp
				
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
				
				popret ra
				nop

;======================================================
; BK_dodupl  arg0, arg1
;======================================================

BK_dodupl
				
				pushreg ra
				
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
				
				