#public QuickResponse Server(string server) => AddHeader("server", server);


fd = open('genquickrespmethods.txt', 'r')
lines = fd.readlines()
fd.close()

comment = None

for line in lines:
    if line[0] == '*':
        comment = line[1:].strip()
    else:
        field = line.strip()

        field_safe = field.replace('-', '')
        if comment is not None:
            print('/// <summary>')
            print('/// %s' % comment)y
            print('/// </summary>')
            comment = None
        print('public QuickResponse %s(string v) => AddHeader("%s", v);' % (field_safe, field))
