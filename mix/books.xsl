<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:template match="library">
		<html>
			<head>
				<title>Noxico Books</title>
			</head>
			<body>
				<h1>Books</h1>
				<xsl:apply-templates select="book" />
			</body>
		</html>
	</xsl:template>

	<xsl:template match="book">
		<h2>
			<xsl:value-of select="@id" />. <xsl:value-of select="@title" />
		</h2>
		<xsl:apply-templates select="identify" />
		<xsl:apply-templates select="p" />
	</xsl:template>

	<xsl:template match="identify">
		<p><code>[<xsl:value-of select="@token" />]</code></p>
	</xsl:template>	

	<xsl:template match="p">
		<p>
			<xsl:apply-templates />
		</p>
	</xsl:template>

	<xsl:template match="b">
		<b>
			<xsl:apply-templates />
		</b>
	</xsl:template>

	<xsl:template match="br">
		<br />
	</xsl:template>

</xsl:stylesheet>