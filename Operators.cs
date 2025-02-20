using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace asmpp
{
	public class Operators
	{
		// https://en.wikipedia.org/wiki/X86_instruction_listings
		public static string Mov(Token SizeTypeA, Token a, Token SizeTypeB, Token b)
		{
			// TODO: Double check all edge case error handling
			// 0 = unknown symbol, 1 = register, 2 = memory location, 3 = memory reference
			int aType = a.type == TokenType.memoryReference ? 3 : a.type == TokenType.memoryLocation ? 2 : Registers.IsRegister(a.value) ? 1 : 0;
			int bType = b.type == TokenType.memoryReference ? 3 : b.type == TokenType.memoryLocation ? 2 : Registers.IsRegister(b.value) ? 1 : 0;

			if (aType == 0)
			{
				if (int.TryParse(a.value, out int _))
				{
					// Left and parameter is an inager literal
					throw new Exception($"Error at {a.line}:{a.start}: Left hand parameter cannot be an integer literal");
				}
				// Left hand parameter is not a register, integer literal or a reserved space in memory
				throw new Exception($"Error at {a.line}:{a.start}: Unknown symbol {a.value}");
			}
			else if (aType == 2)
			{
				throw new Exception($"Error at {a.line}:{b.line}: Left hand parameter cannot be a memory location");
			}
			// Right hand parameter check
			if (bType == 0)
			{
				if (!int.TryParse(b.value, out int _))
				{
					// Right and parameter is an integer literal
					throw new Exception($"Error at {b.line}:{b.start}: Right hand parameter cannot be an integer literal");
				}
				// Right hand parameter is not a register, integer literal or a reserved space in memory
				throw new Exception($"Error at {b.line}:{b.start}: Unknown symbol {b.value}");
			}

			if ((aType == 3) && (bType == 3))
			{
				// Both operands are memory addresses
				throw new Exception($"Error at {b.line}:{b.start}: Both operands connot be memory locations");
			}

			if (Consts.NumBytes(SizeTypeA.value, 1) != Registers.NumBytes(b))
			{
				throw new Exception($"Error at {b.line}:{b.start}: size of right hand operand must match the left");
			}


			//Functions.IsVar()


			return $"";
		}
	}
}
