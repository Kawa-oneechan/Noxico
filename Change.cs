using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public class Change : TokenCarrier
	{
		public List<bool> apply(Character target)
		{
			List<bool> returns = new List<bool>();
			foreach (Token change in this.Tokens)
			{
				if (change.Name.StartsWith("!"))
				{
					change.Name = change.Name.Substring(1);
					foreach (Token checktoken in target.Tokens)
					{
						if (checktoken.Equals(change))
						{
							target.RemoveToken(checktoken);
							returns.Add(true);
							break;
						}
					}
					returns.Add(false);
				}
				else
				{
					switch (change.Name)
					{
						case "deltaBreastSize":
							target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value).GetToken("size").Value += change.GetToken("size").Value;
							returns.Add(true);
							break;
						case "deltaBreastNum":
							Token boob = target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value);
							boob.GetToken("amount").Value += change.GetToken("amount").Value;
							if (boob.GetToken("amount").Value == 0)
								target.RemoveToken(boob);
							target.FixBroken();
							returns.Add(true);
							break;
						case "dicknipples":
							Token nips = target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value).GetToken("nipples");
							if (nips != null)
							{
								if (!nips.HasToken("canfuck"))
								{
									nips.RemoveToken("fuckable");
									nips.RemoveToken("wetness");
									nips.RemoveToken("looseness");
									nips.AddToken("canfuck");
									nips.AddToken("length", change.GetToken("length").Value);
									nips.AddToken("thickness", change.GetToken("thickness").Value);
									returns.Add(true);
								}
								else
									returns.Add(false);
							}
							else
								returns.Add(false);
							break;
						case "nipplecunts":
							Token nipples = target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value).GetToken("nipples");
							if (nipples != null)
							{
								if (!nipples.HasToken("fuckable"))
								{
									nipples.RemoveToken("canfuck");
									nipples.RemoveToken("length");
									nipples.RemoveToken("thickness");
									nipples.AddToken("fuckable");
									nipples.AddToken("wetness", change.GetToken("wetness").Value);
									nipples.AddToken("looseness", change.GetToken("looseness").Value);
									returns.Add(true);
								}
								else
									returns.Add(false);
							}
							else
								returns.Add(false);
							break;
						case "deltaCockLength":
							target.GetPenisByNumber((int)change.GetToken("index").Value).GetToken("length").Value += change.GetToken("length").Value;
							returns.Add(true);
							break;
						case "deltaCockThickness":
							target.GetPenisByNumber((int)change.GetToken("index").Value).GetToken("thickness").Value += change.GetToken("thickness").Value;
							returns.Add(true);
							break;
						case "deltaNippleSize":
							Token boobs = target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value);
							if (boobs.HasToken("nipples"))
							{
								boobs.GetToken("nipples").GetToken("size").Value += change.GetToken("size").Value;
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "deltaNippleNumber":
							Token boobies = target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value);
							if (boobies.HasToken("nipples"))
							{
								boobies.GetToken("nipples").Value += change.GetToken("amount").Value;
								returns.Add(true);
							}
							else
							{
								boobies.AddToken("nipples", change.GetToken("amount").Value);
								returns.Add(true);
							}
							break;
						case "taur":
							if (!target.HasToken("taur"))
							{
								target.AddToken("taur", (int)change.Value + target.GetToken("quadruped").Value);
								target.RemoveToken("snaketail");
								target.RemoveToken("slimeblob");
								target.RemoveToken("quadruped");
								target.FixBroken();
								returns.Add(true);
							}
							else
							{
								target.GetToken("taur").Value += (int)change.Value;
								if (target.GetToken("taur").Value <= 0)
								{
									target.RemoveToken("taur");
									target.FixBroken();
								}
								returns.Add(true);
							}
							break;
						case "snaketail":
							if (!target.HasToken("snaketail"))
							{
								target.RemoveToken("taur");
								target.RemoveToken("slimeblob");
								target.RemoveToken("quadruped");
								target.AddToken("snaketail");
								target.FixBroken();
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "quadruped":
							if (!target.HasToken("quadruped"))
							{
								target.AddToken("quadruped", change.Value + target.GetToken("taur").Value);
								target.RemoveToken("taur");
								target.RemoveToken("slimeblob");
								target.RemoveToken("snaketail");
								target.FixBroken();
								returns.Add(true);
							}
							else
							{
								target.GetToken("quadruped").Value += (int)change.Value;
								if (target.GetToken("quadruped").Value <= 0)
								{
									target.RemoveToken("quadruped");
									target.FixBroken();
								}
								returns.Add(true);
							}
							break;
						case "slimeblob":
							if (!target.HasToken("slimeblob"))
							{
								target.RemoveToken("taur");
								target.RemoveToken("snaketail");
								target.RemoveToken("quadruped");
								target.AddToken("slimeblob");
								target.FixBroken();
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "normalLegs":
							if (target.HasToken("taur") || target.HasToken("snaketail") || target.HasToken("slimeblob") || target.HasToken("quadruped"))
							{
								target.RemoveToken("taur");
								target.RemoveToken("snaketail");
								target.RemoveToken("quadruped");
								target.RemoveToken("slimeblob");
								target.FixBroken();
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;

						default:
							target.AddToken(change);
							returns.Add(true);
							break;
					}
				}
			}
			return returns;
		}

		public Change(List<Token> toks)
		{
			foreach (Token t in toks)
			{
				this.Tokens.Add(t);
			}
		}

		public Change(Token t)
		{
			this.Tokens.Add(t);
		}
	}
}
