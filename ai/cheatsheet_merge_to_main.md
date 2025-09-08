# Merge feature branch to main

```
git checkout main
git merge --squash 20250812_sm

# tortoisegit commit
# - revert and delete claude files and ai folder
#   (and everything else you don't want on github)
# - clean commit message
# - commit

git checkout 20250812_sm
git merge main

# DONE
```