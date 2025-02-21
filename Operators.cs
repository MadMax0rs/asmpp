using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace asmpp
{
	public class Operators
	{
		private enum operandType : byte
		{
			unknownSymbol,
			register,
			/// <summary>
			/// a location in memory, but not the memory at the location
			/// </summary>
			memoryAddress,
			/// <summary>
			/// the memory at a specified location
			/// </summary>
			memoryReference,
			/// <summary>
			/// integer litteral
			/// </summary>
			int_lit
		}
		// https://en.wikipedia.org/wiki/X86_instruction_listings
		public static string Mov(Token a, Token b)
		{
			// 0 = unknown symbol, 1 = register, 2 = memory location, 3 = memory reference
			operandType aType =
				a.type == TokenType.memoryReference ? operandType.memoryReference :
				a.type == TokenType.memoryAddress ?
				// If left hand operand is a memory location
				throw new Exception($"Error at {a.line}:{a.line}: Left hand parameter cannot be a memory location") :
				Registers.IsRegister(a.value) ? operandType.register :
				int.TryParse(a.value, out int _) ?
				// Left hand parameter is an inager literal
				throw new Exception($"Error at {a.line}:{a.start}: Left hand parameter cannot be an integer literal") :
				// If left param is an unknown symbol
				throw new Exception($"Error at {a.line}:{a.start}: Unknown symbol {a.value}");
			
			operandType bType =
				b.type == TokenType.memoryReference ? operandType.memoryReference :
				b.type == TokenType.memoryAddress ? operandType.memoryAddress :
				Registers.IsRegister(b.value) ? operandType.register :
				int.TryParse(b.value, out int _) ? operandType.int_lit :
				// Right hand parameter is not a register, integer literal or a reserved space in memory
				throw new Exception($"Error at {b.line}:{b.start}: Unknown symbol {b.value}");

			// Cannot move from one memory location directly to another
			if ((aType == operandType.memoryReference) && (bType == operandType.memoryReference))
			{
				// Both operands are memory addresses
				throw new Exception($"Error at {b.line}:{b.start}: Both operands connot be memory locations");
			}
			// Both operands must be the same size in bytes
			else if (a.size != Registers.NumBytes(b))
			{
				throw new Exception($"Error at {b.line}:{b.start}: size of right hand operand must match the left");
			}

			// Operation is good and passed all checks

			if (aType == operandType.memoryReference)
			{
				// Left hand operand is a reference to a location in memory
				uint maxBits = (uint)a.size * 8;
				if (bType == operandType.int_lit)
				{
					if (Math.Pow(2, maxBits) >= b.size)
					return 
				}
			}
			else if (bType == operandType.memoryReference)
			{
				// Right hand operand is a reference to a location in memory
			}
			else if (aType == operandType.register)
			{
				// Left hand operand is a register
			}


			return $"";
		}
	}
}
