function SpeechFilter(s)
	-- Don't have gsub, use .Net String's Replace instead.
	s = s.Replace("gr", "<<GR>>")
	s = s.Replace("ng", "<<NG>>")
	s = s.Replace("g", "k")
	s = s.Replace("<<GR>>", "gr")
	s = s.Replace("<<NG>>", "ng")
	return s
end
