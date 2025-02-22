// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using asmpp;

namespace asmpp
{
	internal class Program
	{
		public static string FileString = "";
		public static List<Token> Tokens = [];
		public static List<Var> Vars = [];
		public static string Output = "";

		public static string entryPoint = "";

		public static uint selectedCommandLineOptions = 0b_0000;
		static void Main(string[] args)
		{
			if (args.Length <= 0)
			{
				Console.WriteLine("Error: no filePath given, type asmpp -? for help");
				return;
			}

			if (args[0] == "-?")
			{
				Console.WriteLine("asmpp help:\nasmpp <File Path> \t\tcompile asmpp file\nasmpp -?\t\t print the asmpp help page");
				return;
			}

			string filePath = args[0];

			if (!File.Exists(filePath))
			{
				Console.WriteLine($"Error: File at path {filePath} does not exist");
				return;
			}


			// Start i at 1 to exclude the filepath
			for (int i = 1; i < args.Length; i++)
			{
				switch (args[i])
				{
					// Show stacktrace in error output
					case "-st":
						selectedCommandLineOptions |= (uint)CommandLineOptions.showStacktrace;
						break;
					case "-entry":
						i++;
						if (i == args.Length)
						{
							throw new Exception("Error in asmpp invocation: no entrypoint given, please use '-entry entryPointName' to define an entry point");
						}
						entryPoint = args[i];
						selectedCommandLineOptions |= (uint)CommandLineOptions.entryPointDefined;
						break;
					case "-x86":
						break;
					case "-x64":
						break;
					default:
						Console.WriteLine($"unrecognized command line argument {args[i]}");
						return;
				}
			}
			if ((selectedCommandLineOptions & (uint)CommandLineOptions.entryPointDefined) != (uint)CommandLineOptions.entryPointDefined)
			{
				throw new Exception("Error in asmpp invocation: no entrypoint given, please use '-entry entryPointName' to define an entry point");
			}

			FileString = File.ReadAllText(filePath);
			try
			{
				List<Token> tokens = SplitString(FileString);
				Tokens = Tokenize(tokens);
				Output = TokensToAsm(Tokens, FileString);
				string compiledPath = args[0][..^2];
				if (File.Exists(compiledPath))
				{
					File.Delete(compiledPath);
				}
				File.Create(compiledPath).Close();
				File.WriteAllText(compiledPath, Output);
			}
			catch (Exception e)
			{
				Output = $"{e.Message}";
				if ((selectedCommandLineOptions & (uint)CommandLineOptions.showStacktrace) == 0b_0001)
				{
					Output += $"\nStacktrace:\n{e.StackTrace}";
				}
			}
			try
			{
				Console.Write($"{Output}\n");
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
			return;
		}

		public static List<Token> SplitString(string code)
		{
			code = code.Replace("\r\n", "\n");
			string[] splitCode = Regex.Split(code, @"([\s'""{}\(\)\[\],;\\=*/=\-+<>])").Where(s => s != "").ToArray();
			List<Token> tokens = [];
			int line = 1;
			int column = 0;

			for (int i = 0; i < splitCode.Length; i++)
			{
				string str = splitCode[i];
				if (str == "\n")
				{
					line++;
					column = 0;
					continue;
				}
				Token currentToken = new(value: str, start: column, line: line);
				column += str.Length;
				currentToken.end = column;
				tokens.Add(currentToken);
			}

			return tokens;
		}

		public static List<Token> Tokenize(List<Token> tokensIn)
		{
			List<Token> tokensOut = [];
			bool stringLiteral = false;
			bool escapeChar = false;
			bool arg = false;
			Token currentToken = new(start: 0, line: 0);
			int lastCheckedToken = -1;

			for (int i = 0; i < tokensIn.Count; i++)
			{
				if (tokensIn[i].value == "")
				{
					throw new Exception("Error: Why did I find a blank string... blank strings should've been removed... 😬😬😬");
				}

				if (stringLiteral)
				{
					if (tokensIn[i].value == @"[\\][rnts""]" && !escapeChar)
					{
						currentToken.value = Regex.Unescape(currentToken.value);
						tokensOut.Add(currentToken);
						stringLiteral = false;
						continue;
					}
					escapeChar = false;
					if (tokensIn[i].value.EndsWith('\\'))
					{
						escapeChar = true;
					}
					currentToken.value += tokensIn[i].value;
					currentToken.end = tokensIn[i].end;

					continue;
				}
				// Set current token after string literal so that, when dealing with a string literal, it keeps its value, start, and line
				currentToken = tokensIn[i];
				int next;
				switch (tokensIn[i].value)
				{
					// Keywords
					case "extern":
						next = Functions.FindNextNonWhitespace(tokensIn, i + 1);
						currentToken = tokensIn[next];
						currentToken.type = TokenType._extern;
						tokensOut.Add(currentToken);
						i = next;
						break;

					case "return":
						next = Functions.FindNextNonWhitespace(tokensIn, i + 1);
						currentToken = tokensIn[next];
						currentToken.type = TokenType._return;
						tokensOut.Add(currentToken);
						i = next;
						break;

					case "section":
						next = Functions.FindNextNonWhitespace(tokensIn, i + 1);
						currentToken = tokensIn[next];
						currentToken.type = TokenType.section;
						tokensOut.Add(currentToken);
						i = next;
						break;

					case "global":
						currentToken.type = TokenType.global;
						tokensOut.Add(currentToken);
						break;
					case "label":
						next = Functions.FindNextNonWhitespace(tokensIn, i + 1);
						currentToken = tokensIn[next];
						currentToken.type = TokenType.label;
						tokensOut.Add(currentToken);
						i = next;
						break;

					case "(":
						currentToken.type = TokenType.argsStart;
						tokensOut.Add(currentToken);
						arg = true;
						int j = 1;
						while (tokensIn[i - j].value.Length < 1 || !char.IsWhiteSpace(tokensIn[i - j].value[0]))
						{
							if (i - j <= 0)
							{
								throw new Exception($"Error at symbol '(' at {currentToken.line}:{currentToken.start}: Unable to find function name to call");
							}
							j++;
						}
						j--;
						currentToken = tokensIn[i - j];
						currentToken.type = TokenType.funcCall;
						tokensOut.Insert(tokensOut.Count - 1, currentToken);
						break;

					case ")":
						if (tokensOut[^1].type != TokenType.argsStart && tokensOut[^1].type != TokenType.arg)
						{
							throw new Exception($"Error at {currentToken.line}:{currentToken.start}: Unexpected symbol ')'");
						}
						currentToken.type = TokenType.argsEnd;
						tokensOut.Add(currentToken);
						arg = false;
						break;

					case "[":
						// TODO: Double check case is correct
						Token lastKeyword = tokensIn[Functions.FindLastNonWhitespace(tokensIn, i - 1)];
						int varIndex = Functions.FindNextNonWhitespace(tokensIn, i + 1);
						Token var = tokensIn[varIndex];
						// If the last non-whitespace is a valid sized type(byte, word, etc.)
						if (Consts.SizedTypes.Contains(lastKeyword.value))
						{
							lastKeyword.type = TokenType.sizedType;
							tokensOut.Add(lastKeyword);
							var.size = (uint?)Consts.NumBytes(lastKeyword.value, 1);
							var.sizeBits = var.size * 8;
							var.sizeType = lastKeyword.value;
						}
						else
						{
							var.size = null;
						}



						var.type = TokenType.memoryReference;
						tokensOut.Add(var);

						i = Functions.FindNextNonWhitespace(tokensIn, varIndex + 1);
						if (tokensIn[i].value != "]")
						{
							throw new Exception($"Error at {tokensIn[i].line}:{tokensIn[i].start}: Expected ']' but got {tokensIn[i].value}");
						}
						i++;

						break;
					case "]":
						throw new Exception($"Error at {tokensIn[i].line}:{tokensIn[i].start}: Unexpected ']'");

					case ",":
						if (!arg)
						{
							throw new Exception($"Error at symbol ',' at {currentToken.line}:{currentToken.start}: Unexpected symbol ','");
						}

						break;

					// Semicolon(;) - End of line
					case ";":
						currentToken.type = TokenType.semi;
						tokensOut.Add(currentToken);
						break;

					// Double Quote(") - Start string literal
					case "\"":
						stringLiteral = true;
						currentToken.type = TokenType.str_lit;
						break;
					// Ingore Characters({}) - Chars that hold no real purpose for the final compilation, but could still cause errors if used incorrectly
					case "{":
					case "}":
						currentToken.type = TokenType.ignoreChar;
						tokensOut.Add(currentToken);
						break;
					// Whitespace characters
					case " ":
					case "\t":
					case "\n":
					case "\r":
					case "\v":
						continue;
					// Operators
					case "<":
						if (tokensIn[i + 1].value == "<")
						{
							i++;
							next = Functions.FindNextNonWhitespace(tokensIn, i);

						}
						break;
					case ">":
						if (tokensIn[i + 1].value == ">")
						{
							i++;
							next = Functions.FindNextNonWhitespace(tokensIn, i);

						}
						break;
					case "+":
						if (tokensIn[i + 1].value == "+")
						{
							i++;
							next = Functions.FindNextNonWhitespace(tokensIn, i);

						}
						break;
					case "-":
						if (tokensIn[i + 1].value == "-")
						{
							i++;
							next = Functions.FindNextNonWhitespace(tokensIn, i);

						}
						break;
					case "=":
						{
							if (i < 1)
							{
								throw new Exception($"Error at {currentToken.line}:{currentToken.start}: no specified register to write to");
							}

							j = 2;
							currentToken.type = TokenType._operator;
							if (tokensIn[i - 1].value == "*" || tokensIn[i - 1].value == "/" || tokensIn[i - 1].value == "-" || tokensIn[i - 1].value == "+")
							{
								currentToken.value = tokensIn[i - 1].value + currentToken.value;
							}
							else
							{

								j = 1;
							}

							Token tmp = tokensIn[Functions.FindLastNonWhitespace(tokensIn, i - j)];
							tmp.type =
								Registers._8Bit.Contains(tmp.value) ? TokenType._8BitRegister :
								Registers._16Bit.Contains(tmp.value) ? TokenType._16BitRegister :
								Registers._32Bit.Contains(tmp.value) ? TokenType._32BitRegister :
								Registers._64Bit.Contains(tmp.value) ? TokenType._64BitRegister :
								throw new Exception($"Error at {tmp.line}:{tmp.start}: Invalid register {tmp.value}");

							tokensOut.Add(currentToken);
							tokensOut.Add(tmp);
							currentToken = tokensIn[Functions.FindNextNonWhitespace(tokensIn, i + 1)];

							currentToken.type =
								Registers._8Bit.Contains(currentToken.value) ? TokenType._8BitRegister :
								Registers._16Bit.Contains(currentToken.value) ? TokenType._16BitRegister :
								Registers._32Bit.Contains(currentToken.value) ? TokenType._32BitRegister :
								Registers._64Bit.Contains(currentToken.value) ? TokenType._64BitRegister :
								int.TryParse(currentToken.value, out int _) ? TokenType.int_lit :   // Don't need the number from the parse, only the successfullness of the operation
								throw new Exception($"Error at {currentToken.line}:{currentToken.start}: Invalid register {currentToken.value}");

							tokensOut.Add(currentToken);
							i++;
							break;
						}
					default:
						if (arg)
						{
							currentToken.type = TokenType.arg;
						}
						if (Functions.IsVar(currentToken.value))
						{
							currentToken.type = TokenType.memoryAddress;
						}
						if (Registers.IsRegister(currentToken.value))
						{
							currentToken.type = Registers.RegisterToType(currentToken.value);
						}
						if (int.TryParse(currentToken.value, out _))
						{
							currentToken.type = TokenType.int_lit;
							currentToken.sizeBits = Functions.NumBitsForInt(uint.Parse(currentToken.value));
						}

						// token type is TokenType.none by default
						tokensOut.Add(currentToken);
						break;
				}

				lastCheckedToken = i;
			}

			return tokensOut;
		}


		public static string TokensToAsm(IList<Token> tokens, string code)
		{
			string output = "";
			List<TokenType> expectType = [];
			string currentFuncCall = "";
			Stack currentSectionStack = new();
			currentSectionStack.Push(SectionType.none);
			string args = "";
			for (int i = 0; i < tokens.Count; i++)
			{
				Token token = tokens[i];
				if (expectType.Count > 0)
				{
					if (expectType[0] != token.type)
					{
						throw new Exception($"Error at {token.line}:{token.start}: expected '{Consts.TokenTypeStrings[(int)expectType[0]]}' but got '{token.value}'");
					}
					expectType.RemoveAt(0);
				}
				if (currentSectionStack.Count > 0 && (SectionType)currentSectionStack.Peek() == SectionType.vars)
				{
					switch (token.type)
					{
						case TokenType.sizedType:
							expectType.Add(TokenType.int_lit);
							expectType.Add(TokenType.none);
							expectType.Add(TokenType.semi);
							// args = "dword ";
							args = $"{Consts.SizedTypesBss[Array.IndexOf(Consts.SizedTypes, token.type)]} ";
							break;
						case TokenType.int_lit:
							// args = "dword 1";
							args += $"{token.type}";
							break;
						case TokenType.none:
							// output += "stdOutHandle resd 1
							output += $"{token.value} {args}\n";
							args = "";
							Vars.Add(new Var());
							break;
						case TokenType.semi:
							break;
						default:
							throw new Exception($"Error at {token.line}:{token.start}: Expected a sized type (byte, word, dword, qword, etc.), but got '{token.value}'");
					}
					continue;
				}
				switch (token.type)
				{
					case TokenType._extern:
						if (currentSectionStack.Count != 1)
						{
							throw new Exception($"Error at {token.line}:{token.start}: externs must be defined outside of any section");
						}
						expectType.Add(TokenType.semi);
						output += $"extern {token.value}\n";
						break;
					case TokenType._return:
						if ((SectionType)currentSectionStack.Peek() != SectionType.prgm)
						{
							throw new Exception($"Error at {token.line}:{token.start}: Invalid keyword in current section \"{currentSectionStack.Peek()}\"");
						}
						if (i + 2 < tokens.Count && tokens[i + 2].type == TokenType.semi)
						{
							output += $"push {tokens[i + 1].value}\nret\n";
						}
						else if (tokens[i + 1].type == TokenType.semi)
						{
							output += "ret\n";
						}
						else
						{
							throw new Exception(
								$"Error: missing semicolon on line {tokens[i + 2].line}\n" +
								$"return {tokens[i + 1].value}");
						}
						i += 2;
						break;
					case TokenType.section:
						if (tokens[i + 1].value != "{")
						{
							throw new Exception(
								$"Error at {token.line}:{token.start}: Expected '{{' but got '{tokens[i + 1].value}'\n" +
								$"section {tokens[i + 1].value}\n" +
								$"        {String.Concat(Enumerable.Repeat(" ", tokens[i + 1].value.Length))}^");
						}
						if ((SectionType)currentSectionStack.Peek() != SectionType.none)
						{
							throw new Exception($"Error at {token.line}:{token.start}: Expected '}}' but got section");
						}
						switch (token.value)
						{
							case "data":
								output += "section .data\n";
								currentSectionStack.Push(SectionType.data);
								break;
							case "vars":
								output += "section .bss\n";
								currentSectionStack.Push(SectionType.vars);
								break;
							case "prgm":
								output += "section .text\n";
								currentSectionStack.Push(SectionType.prgm);
								break;
						}
						i += 1;
						break;
					case TokenType.int_lit:
						throw new Exception(
							$"Error, random integer literal at {token.line}:{token.start}\n" +
							$"{token.value}\n" +
							String.Concat(Enumerable.Repeat("^", token.value.Length)));
					case TokenType.funcCall:
						currentFuncCall = token.value;
						break;
					case TokenType.global:
						if (tokens[i + 1].type != TokenType.label)
						{
							throw new Exception($"Error at {tokens[i + 1].line}:{tokens[i + 1].start}: Expected keyword 'label' but got {tokens[i + 1].value}");
						}
						output += $"global {tokens[i + 1].value}\n";
						expectType.Add(TokenType.label);
						break;
					case TokenType.label:
						currentSectionStack.Push(SectionType.label);
						output += $"{token.value}:\n";
						expectType.Add(TokenType.ignoreChar);
						break;
					case TokenType.argsStart:
						break;
					case TokenType.argsEnd:
						expectType.Add(TokenType.semi);
						string joinedStr = String.Join("", args.Reverse());
						output += $"{joinedStr}call {currentFuncCall}\n";
						args = "";
						currentFuncCall = "";
						break;
					case TokenType.arg:
						// Reverse returns an IEnumerable<char>, have to use String.Join with a blank string to convert it back to a string,
						// using ToString() returns type name
						args += String.Join("", $"push {token.value}\n".Reverse());
						break;
					case TokenType.semi:
						//output += "\r\n";
						break;
					case TokenType.keyword:
						//Output += token.value;
						throw new Exception($"Error at {token.line}:{token.start}: Unhandled keyword \"{token.value}\"");
					case TokenType.ignoreChar:
						if (token.value == "}")
						{
							if (currentSectionStack.Count <= 1)//(SectionType)currentSectionStack.Peek() != SectionType.none)
							{
								throw new Exception($"Error at {token.line}:{token.start}: Unexpected symmbol '}}'");
							}
							currentSectionStack.Pop();
						}
						break;
					case TokenType._operator:
						bool op0IsVar = !Registers.IsRegister(tokens[i + 1]);
						bool op1IsVar = !Registers.IsRegister(tokens[i + 2]);
						if (op0IsVar || op1IsVar)
						{
							switch (token.value)
							{
								case "=":
									if (op0IsVar && op1IsVar)
									{
										throw new Exception($"Error at {token.line}:{token.start}: Operator '=' cannot have 2 ");
									}
									break;
								default:
									throw new Exception($"Error at {token.line}:{token.start}: Operator '{token.value}' cannot take a ");
							}
						}
						switch (token.value)
						{
							case "+=":
								output += $"add {tokens[i + 1].value}, {tokens[i + 2].value}\n";
								break;
							case "-=":
								output += $"sub {tokens[i + 1].value}, {tokens[i + 2].value}\n";
								break;
							case "/=":
								if (!Registers.IsRegister(tokens[i + 1].value))
								{
									throw new Exception($"Error at {token.line}:{token.start}: Cannot divide a value in memory");
								}
								else if (!Registers.IsRegister(tokens[i + 2].value))
								{
									throw new Exception($"Error at {token.line}:{token.start}: Cannot divide by a value in memory");
								}
								// TODO: Improve if possible
								char? registerPrefix = '\0';
								string pushString = ""; // Contains all push statements at beginning of operation to store registers
														// and mov statements to move the correct values into the correct registers
								string movString = ""; // Contains string after div statement 
								string popString = ""; // Contains all pop statements to reset the pushed and moved registers
								string divBy = tokens[i + 2].value; // Register to devide by

								if ((selectedCommandLineOptions & (uint)CommandLineOptions.x64) == (uint)CommandLineOptions.x64)
								{
									// Compile to 64-bit
									registerPrefix = 'r';
								}
								else
								{
									// Compile to 32-bit
									registerPrefix = 'e';
								}

								// tokens[i + 1] operator tokens[i + 2]
								// eg.  eax         /=         ecx
								if (tokens[i + 1].value != $"{registerPrefix}ax")
								{
									pushString +=
										$"push {registerPrefix}ax\n" +
										$"mov {registerPrefix}ax, {tokens[i + 1].value}\n";
									movString += $"mov {tokens[i + 1]}, {registerPrefix}ax\n";
									popString += $"pop {registerPrefix}ax\n";
								}

								if (tokens[i + 2].type == TokenType.int_lit)
								{
									pushString +=
										$"push {registerPrefix}bx\n" +
										$"mov {registerPrefix}bx, {tokens[i + 2].value}\n";
									popString = $"pop {registerPrefix}bx\n" + popString;

									divBy = $"{registerPrefix}bx";
								}

								output += $"{pushString}div {divBy}\n{movString}{popString}";
								break;
							case "*=":
								if (!Registers.IsRegister(tokens[i + 1].value))
								{
									throw new Exception($"Error at {token.line}:{token.start}: Cannot multiply a value in memory");
								}
								else if (!Registers.IsRegister(tokens[i + 2].value))
								{
									throw new Exception($"Error at {token.line}:{token.start}: Cannot multiply by a value in memory");
								}
								// TODO: Improve if possible
								registerPrefix = '\0';
								pushString = ""; // Contains all push statements at beginning of operation to store registers
												 // and mov statements to move the correct values into the correct registers
								movString = ""; // Contains string after div statement 
								popString = ""; // Contains all pop statements to reset the pushed and moved registers
								string mulBy = tokens[i + 2].value; // Register to devide by
								Registers.RegisterSizes inputSize;     // User will give register name in size of current program,
								Registers.RegisterSizes necessarySize; // but multiplication can only happen with a register of half
																	   // the size of the current program size.
																	   // ex. an x86 assembly program can only do multiplication
																	   // bewtween 2 16 bit registers
																	   // but multiplication can only happen with a register of half

								if ((selectedCommandLineOptions & (uint)CommandLineOptions.x64) == (uint)CommandLineOptions.x64)
								{
									// Compile to 64-bit
									registerPrefix = 'e';
									inputSize = Registers.RegisterSizes._64;
									necessarySize = Registers.RegisterSizes._32;
								}
								else
								{
									// Compile to 32-bit
									registerPrefix = null;
									inputSize = Registers.RegisterSizes._32;
									necessarySize = Registers.RegisterSizes._16;
								}

								// tokens[i + 1] token tokens[i + 2]
								// eg.  eax       *=        ecx
								if (Registers.RegisterConvert(tokens[i + 1], inputSize, necessarySize) != $"{registerPrefix}ax")
								{
									pushString +=
										$"push {registerPrefix}ax\n" +
										$"mov {registerPrefix}ax, {tokens[i + 1].value}\n";
									movString += $"mov {tokens[i + 1].value}, {registerPrefix}ax\n";
									popString += $"pop {registerPrefix}ax\n";
								}

								if (tokens[i + 2].type == TokenType.int_lit)
								{
									mulBy = $"{registerPrefix}bx";
									pushString +=
										$"push {registerPrefix}bx\n" +
										$"mov {registerPrefix}bx, {tokens[i + 2].value}\n";
									popString = $"pop {registerPrefix}bx\n" + popString;
								}

								output += $"{pushString}mul {mulBy}\n{movString}{popString}";
								break;
							case "=":
								output += $"mov {tokens[i + 1].value}, {tokens[i + 2].value}\n";
								break;
							case "&=":
							// TODO: Implement operations
							case "|=":
							case "^=":
								break;
							default:
								break;
						}
						i += 2;
						break;
					default:
						if (Registers.IsRegister(token.value))
						{
							break;
						}
						throw new Exception($"Error at {token.line}:{token.start}: Unkown keyword \"{token.value}\"");
				}
			}
			if (expectType.Count > 0)
			{
				throw new Exception($"Error: Expected '{Consts.TokenTypeStrings[(int)expectType[0]]}' but got end of file");
			}
			return output;
		}
	}


}