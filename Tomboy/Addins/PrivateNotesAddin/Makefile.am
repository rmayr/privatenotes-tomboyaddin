include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 						\
	$(LINK_TOMBOY_EXE)				\
	$(GTKSHARP_LIBS) 				\
	$(LINK_MONO_ADDINS)				\
	$(srcdir)/XmppOtrLibrary.dll	\
	$(srcdir)/Infinote.dll			\
	-r:Mono.Posix

TARGET = $(top_builddir)/bin/addins/PrivateNotes.dll
CSFILES = \
	$(srcdir)/AddinPreferences.cs									\
	$(srcdir)/AddinPreferencesFactory.cs					\
	$(srcdir)/CryptoLayer.cs											\
	$(srcdir)/EncryptedWebdavSyncServer.cs				\
	$(srcdir)/EncryptedWebdavSyncServiceAddin.cs	\
	$(srcdir)/PrivateNotesWebDavShareProvider.cs	\
	$(srcdir)/Program.cs													\
	$(srcdir)/SecureSharingFactory.cs							\
	$(srcdir)/ShareNoteAddin.cs										\
	$(srcdir)/ShareProvider.cs										\
	$(srcdir)/ShareSync.cs												\
	$(srcdir)/SharingAppAddin.cs									\
	$(srcdir)/Util.cs															\
	$(srcdir)/WebDAVClient.cs											\
	$(srcdir)/WebDAVInterface.cs									\
	$(srcdir)/cryptScheme/*.cs										\
	$(srcdir)/gui/*.cs														\
	$(srcdir)/Hyena.Json/*.cs											\
	$(srcdir)/Infinote/*.cs											\
	$(srcdir)/local/*.cs
RESOURCES = \
	-resource:$(srcdir)/PrivateNotes.addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/PrivateNotes.addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

