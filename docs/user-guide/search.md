﻿## Search Box

Movies, Shows, Artists and Music Videos can be searched using the search box next to the ErsatzTV logo.

![Search Box](../images/search-box.png)

## Search Fields

The `title` field of all media types is searched by default if no other field is specified.

### Movies

The following fields are available for searching movies:

- `title`: The movie title
- `genre`: The movie genre
- `tag`: The movie tag (not available with Plex metadata)
- `plot`: The movie plot
- `studio`: The movie studio
- `actor`: An actor from the movie
- `library_name`: The name of the library that contains the movie
- `language`: The movie audio stream language
- `release_date`: The movie release date (YYYYMMDD)
- `type`: Always `movie`

### Shows

The following fields are available for searching shows:

- `title`: The show title
- `genre`: The show genre
- `tag`: The show tag (not available with Plex metadata)
- `plot`: The show plot
- `studio`: The show studio
- `actor`: An actor from the show
- `library_name`: The name of the library that contains the show
- `language`: The show audio stream language
- `release_date`: The show release date (YYYYMMDD)
- `type`: Always `show`

### Artists

The following fields are available for searching artists:

- `title`: The artist name
- `genre`: The artist genre
- `style`: The artist style
- `mood`: The artist mood
- `library_name`: The name of the library that contains the artist
- `type`: Always `artist`

### Music Videos

The following fields are available for searching music videos:

- `title`: The music video title
- `genre`: The music video genre
- `library_name`: The name of the library that contains the music video
- `language`: The music video audio stream language
- `type`: Always `music_video`

## Sample Searches

### Christmas

`plot:christmas`

### Christmas without Horror

`plot:christmas NOT genre:horror`

### 1970's Movies

`type:movie AND release_date:197*`

### 1970's-1980's Comedies

`genre:comedy AND (release_date:197* OR release_date:198*)`

### Lush Music

`mood:lush`