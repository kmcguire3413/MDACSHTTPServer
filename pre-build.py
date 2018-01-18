'''
	This python script performs pre-commit functions.

	(1) Automatically update the version.
'''

'''
	$ cat MDACSAuth.csproj
	<Project Sdk="Microsoft.NET.Sdk">

	  <PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>netcoreapp2.0</TargetFramework>
		<AssemblyVersion>1.1.1.1</AssemblyVersion>
		<FileVersion>2.2.2.2</FileVersion>
	  </PropertyGroup>

	  <ItemGroup>
		<Reference Include="MDACSHTTPServer">
		  <HintPath>..\..\MDACSHTTPServer\bin\Debug\netstandard2.0\MDACSHTTPServer.dll</HintPath>
		</Reference>
	  </ItemGroup>

	</Project>
'''

import subprocess
import xml.etree.ElementTree as et
import datetime
import json
import os.path
import os

def IncrementVersionString(verstr):
	verstr = verstr.split('.')

	major = int(verstr[0])
	minor = int(verstr[1])
	build = int(verstr[2])
	rev = int(verstr[3])

	dt = datetime.date.today()
	# The version is split into two 16-bit fields.
	# major.minor.YMM.DDRRR
	build = (dt.year - 2016) * 100 + dt.month
	rev_rrr = rev - (rev // 1000 * 1000)
	rev = dt.day * 1000 + (rev_rrr + 1)

	return '%s.%s.%s.%s' % (major, minor, build, rev)

def IncrementVersionOnProject():
	buildinfo_path = 'buildinfo.json'
	if os.path.exists(buildinfo_path):
		fd = open(buildinfo_path, 'r')
		buildinfo = json.loads(fd.read())
		fd.close()
	else:
		buildinfo = {
			'version': '0.0.0.0',
		}
		
	buildinfo['version'] = IncrementVersionString(buildinfo['version'])

	fd = open(buildinfo_path, 'w')
	fd.write(json.dumps(buildinfo))
	fd.close()

	nodes = os.listdir()

	for node in nodes:
		if node.find('.nuspec') > -1:
			x = et.parse(node)
			root = x.getroot()
			root.find('.metadata/version').text = buildinfo['version']
			x.write(node)

IncrementVersionOnProject()
