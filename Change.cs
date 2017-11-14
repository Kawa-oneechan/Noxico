using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public class Change : TokenCarrier
	{
		public List<bool> Apply(Character target)
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
							target.FixBoobs();
							target.GetToken("breasts").GetToken("size").Value += change.GetToken("size").Value;
							returns.Add(true);
							break;
						case "deltaBreastNum":
							target.FixBoobs();
							target.GetToken("breasts").GetToken("amount").Value += change.GetToken("amount").Value;
							returns.Add(true);
							break;
						case "dicknipples":
							target.FixBoobs();
							var nips = target.GetToken("breasts").GetToken("nipples");
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
							nips = target.GetToken("breasts").GetToken("nipples");
							if (nips != null)
							{
								if (!nips.HasToken("fuckable"))
								{
									nips.RemoveToken("canfuck");
									nips.RemoveToken("length");
									nips.RemoveToken("thickness");
									nips.AddToken("fuckable");
									nips.AddToken("wetness", change.GetToken("wetness").Value);
									nips.AddToken("looseness", change.GetToken("looseness").Value);
									returns.Add(true);
								}
								else
									returns.Add(false);
							}
							else
								returns.Add(false);
							break;
						case "deltaCockLength":
							if (target.HasToken("penis"))
							{
								target.GetToken("penis").GetToken("length").Value += change.GetToken("length").Value;
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "deltaCockThickness":
							if (target.HasToken("penis"))
							{
								target.GetToken("penis").GetToken("thickness").Value += change.GetToken("thickness").Value;
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "deltaNippleSize":
							target.FixBoobs();
							if (target.GetToken("breasts").HasToken("nipples"))
							{
								target.GetToken("breasts").GetToken("nipples").GetToken("size").Value += change.GetToken("size").Value;
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "deltaNippleNumber":
							target.FixBoobs();
							var boobs = target.GetToken("breasts");
							if (boobs.HasToken("nipples"))
							{
								boobs.GetToken("nipples").Value += change.GetToken("amount").Value;
								if (boobs.GetToken("nipples").Value <= 0)
									boobs.RemoveToken("nipples");
							}
							else
								boobs.AddToken("nipples", change.GetToken("amount").Value).AddToken("size", 0.5f);
							returns.Add(true);
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
						case "deltaBalls":
							if (target.HasToken("balls"))
							{
								target.GetToken("balls").GetToken("amount").Value += change.GetToken("amount").Value;
								returns.Add(true);
							}
							else
							{
								if (change.GetToken("amount").Value > 0)
								{
									target.AddToken("balls").AddToken("amount", (int)change.GetToken("amount").Value);
									returns.Add(true);
								}
								else
									returns.Add(false);
							}
							target.FixBroken();
							break;
						case "deltaBallSize":
							if (target.HasToken("balls"))
							{
								target.GetToken("balls").GetToken("size").Value += change.GetToken("size").Value;
								target.FixBroken();
								returns.Add(true);
							}
							else
								returns.Add(false);
							break;
						case "deltaEyes":
							target.GetToken("eyes").GetToken("count").Value += (int)change.GetToken("count").Value;
							returns.Add(true);
							break;
						case "legs":
							if (target.HasToken("legs"))
							{
								if (target.GetToken("legs").Text == change.Text)
									returns.Add(false);
								else
									returns.Add(true);
								target.GetToken("legs").Text = change.Text;
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

		public Change()
		{
		}
	}
}
