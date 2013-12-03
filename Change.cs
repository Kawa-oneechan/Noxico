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
							target.fixBoobs();
							returns.Add(true);
							break;
						case "dicknipples":
							Token nips = target.GetBreastRowByNumber((int)change.GetToken("rowNumber").Value).GetToken("nipples");
							if (nips != null)
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
						

						default:
							target.AddToken(change);
							returns.Add(true);
							break;
					}
				}
			}
			return returns;
		}
	}
}
