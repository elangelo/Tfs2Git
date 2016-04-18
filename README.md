# Tfs2Git
tools to assist migrating from tfs repo to git repo

## Project to migrate workitems with history from one tfs project to another

### BuildHistoryInDatabase
	Runs through the history of all workitems in tfsproject and stores revisions in database. Just references for future use and dependency sorting
	
### CreateMappingFile
	Creates a mappingfile between workitem of old tfs project and new tfs project to be used with git-tfs
	
### Data
	Database model
	
### GenerateFieldMappings
	Creates mapping between fields that are available on source tfs project and target tfs project, allows discarding of fields no longer in use
	
### CreateWorkItems
	does the actual migration, keeps progress in database created in 'BuildHistoryInDatabase'
	
### CreateAUTHORSfile
	Creates an AUTHORS file that git-tfs can use to transform the tfs author to a git author based on the AD with directory searcher
	
### Utils
	more code.	


## Git-tfs stuff
	1.	[CMD] > git tfs clone http://tfsserver:8080:8080/tfs/DefaultCollection "$/SourceProject/Main" --with-branches --authors=AUTHORS --export --export-work-item-mapping=WORKITEMMAPPING
	2.	git remote add origin http://tfsserver:8080/NewCollection/_git/TargetProject
	3.	git push origin --all
