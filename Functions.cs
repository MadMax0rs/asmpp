using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace asmpp
{
	internal class Functions
	{
		public static int FindNextNonWhitespace(List<Token> tokensIn, int currentIndex)
		{
			// <2 chars | Is whitespace | Output
			// ---------|---------------|-------
			// False    | False         | False
			// False    | True          | False, Error(Should not have whitespace as the first character of a token's value
			// True     | False         | False
			// True     | True          | True
			while (tokensIn[currentIndex].value.Length < 2 && char.IsWhiteSpace(tokensIn[currentIndex].value[0]))
			{
				currentIndex++;
			}
			return currentIndex;
		}
		public static int FindLastNonWhitespace(List<Token> tokensIn, int currentIndex)
		{
			// <2 chars | Is whitespace | Output
			// ---------|---------------|-------
			// False    | False         | False
			// False    | True          | False, Error(Should not have whitespace as the first character of a token's value
			// True     | False         | False
			// True     | True          | True
			while (tokensIn[currentIndex].value.Length < 2 && char.IsWhiteSpace(tokensIn[currentIndex].value[0]))
			{
				currentIndex--;
			}
			return currentIndex;
		}

		public static bool IsVar(string var)
		{
			for (int i = 0; i < Program.Vars.Count; i++)
			{
				if (var == Program.Vars[i].name)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="num"></param>
		/// <returns>The minimum number if bits needed to store a given number</returns>
		public static uint NumBitsForInt(uint num)
		{
			return num == 0 ? 1 : (uint)(BitOperations.Log2(num) + 1);
		}
	}
}
